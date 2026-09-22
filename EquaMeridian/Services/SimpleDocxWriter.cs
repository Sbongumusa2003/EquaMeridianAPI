using System.IO.Compression;
using System.Text;

public enum DocxRowStyle { Normal, GroupHeader, Subtotal, GrandTotal }

public sealed class DocxTableRow
{
    public string[] Cells { get; }
    public DocxRowStyle Style { get; }
    public DocxTableRow(string[] cells, DocxRowStyle style = DocxRowStyle.Normal)
    {
        Cells = cells;
        Style = style;
    }
}

public sealed class DocxKpi
{
    public string Label { get; }
    public string Value { get; }
    public DocxKpi(string label, string value) { Label = label; Value = value; }
}

/// <summary>
/// Branded Word (.docx) report builder: EquaMeridian Holdings letterhead, a KPI summary strip,
/// section headings, and real Word tables — header banner, zebra rows, group/subtotal/grand-total
/// shading — instead of a monospace text dump. No third-party Word libraries required.
/// </summary>
public sealed class DocxReportEngine
{
    // Brand palette (matches the PDF/XLSX exports and the Angular app)
    private const string BrandOrange = "C84B11";
    private const string BrandCharcoal = "1A1A1A";
    private const string BrandGold = "D4A017";
    private const string LightTint = "FFF5F0";
    private const string SubtotalTint = "FFF0E8";
    private const string BorderGrey = "D0D0D0";
    private const string MutedGrey = "666666";

    // A4 portrait -> content width in twips after margins
    private const int PageWidthTwips = 11907;   // A4 width
    private const int MarginTwips = 1134;        // ~0.79"
    private const int ContentWidthTwips = PageWidthTwips - (2 * MarginTwips);

    private readonly StringBuilder _body = new();
    private readonly string _title;
    private readonly string? _subtitle;
    private readonly string _generatedBy;

    public DocxReportEngine(string title, string? subtitle, string generatedBy = "EquaMeridian Admin Portal")
    {
        _title = title;
        _subtitle = subtitle;
        _generatedBy = generatedBy;
        WriteLetterhead();
    }

    // =====================================================================
    // Public block API
    // =====================================================================

    private DocxReportEngine WriteLetterhead()
    {
        _body.Append(Para(new[] { Run("EQUAMERIDIAN HOLDINGS", 32, bold: true, color: BrandCharcoal) },
            spacingAfter: 40));
        _body.Append(Para(new[] { Run("Machinery Marketplace Platform", 16, bold: false, color: BrandOrange) },
            spacingAfter: 160, borderBottomColor: BrandGold, borderBottomSize: 12));

        _body.Append(Para(new[] { Run(_title, 30, bold: true, color: BrandOrange) }, spacingAfter: 60));
        if (!string.IsNullOrWhiteSpace(_subtitle))
            _body.Append(Para(new[] { Run(_subtitle!, 18, bold: false, italic: true, color: MutedGrey) }, spacingAfter: 60));

        string meta = $"Generated {AppTime.Now:dd MMM yyyy, HH:mm} SAST  |  by {_generatedBy}  |  CONFIDENTIAL";
        _body.Append(Para(new[] { Run(meta, 16, bold: false, color: MutedGrey) }, spacingAfter: 220));
        return this;
    }

    public DocxReportEngine AddKpis(params DocxKpi[] kpis)
    {
        if (kpis.Length == 0) return this;

        int colWidth = ContentWidthTwips / kpis.Length;
        var sb = new StringBuilder();
        sb.Append(TableOpen(Enumerable.Repeat(colWidth, kpis.Length).ToArray(), borders: false));

        sb.Append("<w:tr>");
        foreach (var kpi in kpis)
        {
            sb.Append(Cell(colWidth,
                new[]
                {
                    Para(new[] { Run(kpi.Label.ToUpperInvariant(), 13, bold: true, color: MutedGrey) }, spacingAfter: 40),
                    Para(new[] { Run(kpi.Value, 26, bold: true, color: BrandCharcoal) }, spacingAfter: 0)
                },
                shadeFill: LightTint,
                topBorderColor: BrandGold, topBorderSize: 16,
                padding: 120));
        }
        sb.Append("</w:tr>");
        sb.Append("</w:tbl>");
        sb.Append(EmptyParaAfterTable());

        _body.Append(sb);
        return this;
    }

    public DocxReportEngine AddSectionHeading(string text)
    {
        _body.Append(Para(new[] { Run(text, 22, bold: true, color: BrandCharcoal) },
            spacingBefore: 160, spacingAfter: 100,
            borderBottomColor: BrandGold, borderBottomSize: 10));
        return this;
    }

    public DocxReportEngine AddTable(string? sectionTitle, string[] headers, double[] columnWeights, bool[] rightAlign,
        List<DocxTableRow> rows, string? emptyMessage = null)
    {
        if (sectionTitle != null) AddSectionHeading(sectionTitle);

        int[] widths = DistributeWidths(columnWeights, ContentWidthTwips);

        var sb = new StringBuilder();
        sb.Append(TableOpen(widths, borders: true));

        sb.Append("<w:tr><w:trPr><w:tblHeader/></w:trPr>");
        for (int c = 0; c < headers.Length; c++)
        {
            bool ra = rightAlign.Length > c && rightAlign[c];
            sb.Append(Cell(widths[c],
                new[] { Para(new[] { Run(headers[c], 16, bold: true, color: "FFFFFF") }, jc: ra ? "right" : "left") },
                shadeFill: BrandOrange, padding: 80));
        }
        sb.Append("</w:tr>");

        if (rows.Count == 0)
        {
            sb.Append("<w:tr>");
            sb.Append(Cell(ContentWidthTwips,
                new[] { Para(new[] { Run(emptyMessage ?? "No records match the current search or filter criteria.", 16, italic: true, color: MutedGrey) }) },
                padding: 100, gridSpan: headers.Length));
            sb.Append("</w:tr>");
        }

        bool zebra = false;
        foreach (var row in rows)
        {
            string? fill;
            string textColor = "222222";
            bool bold = row.Style != DocxRowStyle.Normal;
            string? topBorderColor = null;

            switch (row.Style)
            {
                case DocxRowStyle.GroupHeader:
                    fill = SubtotalTint;
                    textColor = BrandCharcoal;
                    break;
                case DocxRowStyle.Subtotal:
                    fill = SubtotalTint;
                    textColor = BrandCharcoal;
                    topBorderColor = BrandGold;
                    break;
                case DocxRowStyle.GrandTotal:
                    fill = BrandCharcoal;
                    textColor = "FFFFFF";
                    break;
                default:
                    fill = zebra ? LightTint : null;
                    zebra = !zebra;
                    break;
            }

            sb.Append("<w:tr>");
            for (int c = 0; c < row.Cells.Length && c < widths.Length; c++)
            {
                bool ra = rightAlign.Length > c && rightAlign[c];
                sb.Append(Cell(widths[c],
                    new[] { Para(new[] { Run(row.Cells[c], 16, bold: bold, color: textColor) }, jc: ra ? "right" : "left") },
                    shadeFill: fill, padding: 70,
                    topBorderColor: topBorderColor, topBorderSize: topBorderColor != null ? 10 : 0));
            }
            sb.Append("</w:tr>");
        }

        sb.Append("</w:tbl>");
        sb.Append(EmptyParaAfterTable());

        _body.Append(sb);
        return this;
    }

    public DocxReportEngine AddNote(string text)
    {
        _body.Append(Para(new[] { Run(text, 15, italic: true, color: MutedGrey) }, spacingBefore: 40, spacingAfter: 120));
        return this;
    }

    public byte[] Build()
    {
        _body.Append(Para(new[] { Run("Confidential — EquaMeridian Holdings", 15, italic: true, color: "999999") },
            spacingBefore: 300));

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml());
            WriteEntry(archive, "_rels/.rels", RelsXml());
            WriteEntry(archive, "word/document.xml", DocumentXml());
            WriteEntry(archive, "word/_rels/document.xml.rels", DocRelsXml());
            WriteEntry(archive, "word/styles.xml", StylesXml());
        }
        return stream.ToArray();
    }

    /// <summary>Backward-compatible plain export for callers that just want raw text lines
    /// rendered as a single-column table rather than building a full structured report.</summary>
    public static byte[] Generate(string title, IEnumerable<string> lines, string generatedBy = "EquaMeridian Admin Portal")
    {
        var engine = new DocxReportEngine(title, null, generatedBy);
        var rows = lines.Select(l => new DocxTableRow(new[] { string.IsNullOrEmpty(l) ? " " : l })).ToList();
        engine.AddTable(null, new[] { "Detail" }, new[] { 1.0 }, new[] { false }, rows);
        return engine.Build();
    }

    // =====================================================================
    // OOXML fragment builders
    // =====================================================================

    private static string Run(string text, int halfPoints, bool bold = false, bool italic = false, string color = "222222")
    {
        var props = new StringBuilder("<w:rPr>");
        if (bold) props.Append("<w:b/>");
        if (italic) props.Append("<w:i/>");
        props.Append($"<w:color w:val=\"{color}\"/><w:sz w:val=\"{halfPoints}\"/>");
        props.Append("</w:rPr>");
        return $"<w:r>{props}<w:t xml:space=\"preserve\">{Sanitize(text)}</w:t></w:r>";
    }

    private static string Para(string[] runs, string jc = "left", int spacingBefore = 0, int spacingAfter = 120,
        string? borderBottomColor = null, int borderBottomSize = 0)
    {
        var pPr = new StringBuilder("<w:pPr>");
        pPr.Append($"<w:spacing w:before=\"{spacingBefore}\" w:after=\"{spacingAfter}\"/>");
        if (jc != "left") pPr.Append($"<w:jc w:val=\"{jc}\"/>");
        if (borderBottomColor != null)
            pPr.Append($"<w:pBdr><w:bottom w:val=\"single\" w:sz=\"{borderBottomSize}\" w:space=\"4\" w:color=\"{borderBottomColor}\"/></w:pBdr>");
        pPr.Append("</w:pPr>");
        return $"<w:p>{pPr}{string.Concat(runs)}</w:p>";
    }

    private static string TableOpen(int[] colWidths, bool borders)
    {
        var sb = new StringBuilder("<w:tbl><w:tblPr>");
        sb.Append($"<w:tblW w:w=\"{colWidths.Sum()}\" w:type=\"dxa\"/>");
        sb.Append("<w:tblLayout w:type=\"fixed\"/>");
        if (borders)
        {
            sb.Append("<w:tblBorders>");
            foreach (var edge in new[] { "top", "left", "bottom", "right", "insideH", "insideV" })
                sb.Append($"<w:{edge} w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"{BorderGrey}\"/>");
            sb.Append("</w:tblBorders>");
        }
        sb.Append("</w:tblPr>");
        sb.Append("<w:tblGrid>");
        foreach (var w in colWidths) sb.Append($"<w:gridCol w:w=\"{w}\"/>");
        sb.Append("</w:tblGrid>");
        return sb.ToString();
    }

    private static string Cell(int width, string[] paragraphs, string? shadeFill = null, int padding = 80,
        string? topBorderColor = null, int topBorderSize = 0, int gridSpan = 1)
    {
        var tcPr = new StringBuilder("<w:tcPr>");
        tcPr.Append($"<w:tcW w:w=\"{width}\" w:type=\"dxa\"/>");
        if (gridSpan > 1) tcPr.Append($"<w:gridSpan w:val=\"{gridSpan}\"/>");
        if (shadeFill != null) tcPr.Append($"<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"{shadeFill}\"/>");
        if (topBorderColor != null)
            tcPr.Append($"<w:tcBorders><w:top w:val=\"single\" w:sz=\"{topBorderSize}\" w:color=\"{topBorderColor}\"/></w:tcBorders>");
        tcPr.Append($"<w:tcMar><w:top w:w=\"{padding}\" w:type=\"dxa\"/><w:bottom w:w=\"{padding}\" w:type=\"dxa\"/>" +
                    $"<w:left w:w=\"100\" w:type=\"dxa\"/><w:right w:w=\"100\" w:type=\"dxa\"/></w:tcMar>");
        tcPr.Append("<w:vAlign w:val=\"center\"/>");
        tcPr.Append("</w:tcPr>");
        return $"<w:tc>{tcPr}{string.Concat(paragraphs)}</w:tc>";
    }

    private static string EmptyParaAfterTable() => "<w:p><w:pPr><w:spacing w:after=\"160\"/></w:pPr></w:p>";

    private static int[] DistributeWidths(double[] weights, int totalTwips)
    {
        double sum = weights.Sum();
        if (sum <= 0) sum = weights.Length;
        var widths = weights.Select(w => (int)Math.Round(totalTwips * (w / sum))).ToArray();
        int diff = totalTwips - widths.Sum();
        if (widths.Length > 0) widths[^1] += diff;
        return widths;
    }

    private static string Sanitize(string value) => System.Security.SecurityElement.Escape(value) ?? value;

    // =====================================================================
    // Package parts
    // =====================================================================

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private string DocumentXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
        "<w:body>" + _body +
        $"<w:sectPr><w:pgSz w:w=\"{PageWidthTwips}\" w:h=\"16839\"/>" +
        $"<w:pgMar w:top=\"1134\" w:right=\"{MarginTwips}\" w:bottom=\"1134\" w:left=\"{MarginTwips}\" w:header=\"709\" w:footer=\"709\" w:gutter=\"0\"/>" +
        "</w:sectPr></w:body></w:document>";

    private static string ContentTypesXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
        "<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>" +
        "</Types>";

    private static string RelsXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
        "</Relationships>";

    private static string DocRelsXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
        "</Relationships>";

    private static string StylesXml() =>
        """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:docDefaults>
    <w:rPrDefault><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/><w:sz w:val="20"/></w:rPr></w:rPrDefault>
  </w:docDefaults>
  <w:style w:type="paragraph" w:styleId="Normal" w:default="1">
    <w:name w:val="Normal"/>
  </w:style>
</w:styles>
""";
}

/// <summary>
/// Legacy minimal DOCX writer retained for any caller still using the old static API.
/// New code should use <see cref="DocxReportEngine"/> for branded, tabular Word exports.
/// </summary>
public static class SimpleDocxWriter
{
    public static byte[] Generate(string title, IEnumerable<string> lines, string generatedBy = "EquaMeridian Admin Portal") =>
        DocxReportEngine.Generate(title, lines, generatedBy);
}
