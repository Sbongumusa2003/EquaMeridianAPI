using System.Globalization;
using System.IO.Compression;
using System.Text;

/// <summary>
/// Pure OOXML (.xlsx) writer with EquaMeridian branding: orange header banner, company
/// name, logo-style title, styled column headers, alternating row colours, currency/number
/// formats, freeze panes, auto-filter, and an optional embedded bar chart sheet.
/// No third-party Excel libraries required.
/// </summary>
public static class SimpleXlsxWriter
{
    // Brand colours (match Angular --color-primary and logo palette)
    private const string BrandOrange = "C84B11";
    private const string BrandOrangeDark = "A83D0E";
    private const string BrandGold = "D4A017";
    private const string HeaderBg = "C84B11";
    private const string HeaderFg = "FFFFFF";
    private const string AltRowBg = "FFF5F0";
    private const string TitleBg = "1A1A1A";
    private const string SubtotalBg = "FFF0E8";
    private const string GrandTotalBg = "C84B11";
    private const string BorderColor = "D0D0D0";

    public static byte[] Generate(
        string sheetName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<object?>> rows,
        string? reportTitle = null,
        string? generatedBy = null,
        IReadOnlyList<string>? columnFormats = null)
    {
        reportTitle ??= sheetName;
        generatedBy ??= "EquaMeridian Admin Portal";
        var colCount = Math.Max(headers.Count, 1);
        var safeName = SanitizeSheetName(sheetName);

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml(hasChart: false));
            WriteEntry(archive, "_rels/.rels", RelsXml());
            WriteEntry(archive, "xl/workbook.xml", WorkbookXml(safeName));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml(hasChart: false));
            WriteEntry(archive, "xl/styles.xml", StylesXml());
            WriteEntry(archive, "xl/sharedStrings.xml", SharedStringsXml(headers, rows, reportTitle, generatedBy));
            WriteEntry(archive, "xl/worksheets/sheet1.xml",
                SheetXml(headers, rows, reportTitle, generatedBy, colCount, columnFormats));
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Branded workbook with a data sheet plus a chart sheet showing two series as a
    /// clustered column chart (e.g. Revenue vs Commission). Chart is real Excel chart XML.
    /// </summary>
    public static byte[] GenerateWithBarChart(
        string dataSheetName,
        string chartTitle,
        IReadOnlyList<string> categoryLabels,
        IReadOnlyList<decimal> seriesA,
        string seriesAName,
        IReadOnlyList<decimal> seriesB,
        string seriesBName,
        IReadOnlyList<string> tableHeaders,
        IReadOnlyList<IReadOnlyList<object?>> tableRows,
        string? reportTitle = null,
        string? generatedBy = null)
    {
        reportTitle ??= chartTitle;
        generatedBy ??= "EquaMeridian Admin Portal";
        var colCount = Math.Max(tableHeaders.Count, 1);
        var n = categoryLabels.Count;

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml(hasChart: true));
            WriteEntry(archive, "_rels/.rels", RelsXml());
            WriteEntry(archive, "xl/workbook.xml", WorkbookXmlWithChart(SanitizeSheetName(dataSheetName)));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml(hasChart: true));
            WriteEntry(archive, "xl/styles.xml", StylesXml());
            WriteEntry(archive, "xl/sharedStrings.xml",
                SharedStringsXml(tableHeaders, tableRows, reportTitle, generatedBy, categoryLabels, seriesAName, seriesBName));
            WriteEntry(archive, "xl/worksheets/sheet1.xml",
                SheetXml(tableHeaders, tableRows, reportTitle, generatedBy, colCount, null));
            // Chart data lives on sheet2 in a simple layout: Category | SeriesA | SeriesB
            WriteEntry(archive, "xl/worksheets/sheet2.xml", ChartDataSheetXml(categoryLabels, seriesA, seriesB, seriesAName, seriesBName));
            WriteEntry(archive, "xl/worksheets/_rels/sheet2.xml.rels", ChartSheetRelsXml());
            WriteEntry(archive, "xl/charts/chart1.xml", BarChartXml(chartTitle, n, seriesAName, seriesBName));
            WriteEntry(archive, "xl/drawings/drawing1.xml", DrawingXml());
            WriteEntry(archive, "xl/drawings/_rels/drawing1.xml.rels", DrawingRelsXml());
        }
        return stream.ToArray();
    }

    // -------------------------------------------------------------------------
    // Package parts
    // -------------------------------------------------------------------------

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string ContentTypesXml(bool hasChart) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
        (hasChart
            ? "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
              "<Override PartName=\"/xl/charts/chart1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.drawingml.chart+xml\"/>" +
              "<Override PartName=\"/xl/drawings/drawing1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.drawing+xml\"/>"
            : "") +
        "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
        "<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>" +
        "</Types>";

    private static string RelsXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private static string WorkbookXml(string sheetName) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
        "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
        "<sheets><sheet name=\"" + sheetName + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
        "</workbook>";

    private static string WorkbookXmlWithChart(string dataSheetName) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
        "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
        "<sheets>" +
        $"<sheet name=\"{dataSheetName}\" sheetId=\"1\" r:id=\"rId1\"/>" +
        "<sheet name=\"Chart\" sheetId=\"2\" r:id=\"rId2\"/>" +
        "</sheets></workbook>";

    private static string WorkbookRelsXml(bool hasChart) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
        (hasChart
            ? "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>"
            : "") +
        "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
        "<Relationship Id=\"rId4\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>" +
        "</Relationships>";

    // -------------------------------------------------------------------------
    // Styles — brand palette
    // -------------------------------------------------------------------------

    private static string StylesXml()
    {
        // Style indices used in sheet:
        // 0 = default
        // 1 = title banner (black bg, white bold large)
        // 2 = subtitle (muted)
        // 3 = column header (orange bg, white bold)
        // 4 = normal data
        // 5 = alt row data
        // 6 = currency
        // 7 = currency alt
        // 8 = subtotal row
        // 9 = grand total row (orange, white)
        // 10 = integer
        // 11 = integer alt
        // 12 = percent
        // 13 = percent alt
        // 14 = date
        // 15 = date alt
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <numFmts count="3">
    <numFmt numFmtId="164" formatCode="&quot;R&quot;#,##0.00"/>
    <numFmt numFmtId="165" formatCode="0%"/>
    <numFmt numFmtId="166" formatCode="yyyy-mm-dd"/>
  </numFmts>
  <fonts count="6">
    <font><sz val="11"/><color theme="1"/><name val="Calibri"/></font>
    <font><sz val="18"/><b/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
    <font><sz val="10"/><color rgb="FF666666"/><name val="Calibri"/></font>
    <font><sz val="11"/><b/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
    <font><sz val="11"/><b/><color theme="1"/><name val="Calibri"/></font>
    <font><sz val="11"/><b/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
  </fonts>
  <fills count="7">
    <fill><patternFill patternType="none"/></fill>
    <fill><patternFill patternType="gray125"/></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FF1A1A1A"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFC84B11"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFFFF5F0"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFFFF0E8"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFE8F0FE"/></patternFill></fill>
  </fills>
  <borders count="2">
    <border><left/><right/><top/><bottom/><diagonal/></border>
    <border>
      <left style="thin"><color rgb="FFD0D0D0"/></left>
      <right style="thin"><color rgb="FFD0D0D0"/></right>
      <top style="thin"><color rgb="FFD0D0D0"/></top>
      <bottom style="thin"><color rgb="FFD0D0D0"/></bottom>
      <diagonal/>
    </border>
  </borders>
  <cellStyleXfs count="1"><xf/></cellStyleXfs>
  <cellXfs count="16">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
    <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment horizontal="left" vertical="center"/></xf>
    <xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1"/>
    <xf numFmtId="0" fontId="3" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1"/>
    <xf numFmtId="0" fontId="0" fillId="4" borderId="1" xfId="0" applyFill="1" applyBorder="1"/>
    <xf numFmtId="164" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1"/>
    <xf numFmtId="164" fontId="0" fillId="4" borderId="1" xfId="0" applyNumberFormat="1" applyFill="1" applyBorder="1"/>
    <xf numFmtId="0" fontId="4" fillId="5" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1"/>
    <xf numFmtId="0" fontId="5" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1"/>
    <xf numFmtId="1" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1"/>
    <xf numFmtId="1" fontId="0" fillId="4" borderId="1" xfId="0" applyNumberFormat="1" applyFill="1" applyBorder="1"/>
    <xf numFmtId="164" fontId="4" fillId="5" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1"/>
    <xf numFmtId="164" fontId="5" fillId="3" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyFill="1" applyBorder="1"/>
    <xf numFmtId="166" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1"/>
    <xf numFmtId="166" fontId="0" fillId="4" borderId="1" xfId="0" applyNumberFormat="1" applyFill="1" applyBorder="1"/>
  </cellXfs>
</styleSheet>
""";
    }

    // -------------------------------------------------------------------------
    // Shared strings (for title / headers text)
    // -------------------------------------------------------------------------

    private static string SharedStringsXml(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<object?>> rows,
        string reportTitle,
        string generatedBy,
        IReadOnlyList<string>? extraLabels = null,
        string? seriesAName = null,
        string? seriesBName = null)
    {
        // We use inlineStr for data cells to keep the shared-string table small and
        // predictable; only branding strings go into sharedStrings for the title rows.
        var strings = new List<string>
        {
            "EQUAMERIDIAN HOLDINGS",
            reportTitle,
            $"Generated: {AppTime.Now:yyyy-MM-dd HH:mm} SAST  |  Generated by: {generatedBy}  |  Confidential"
        };
        if (extraLabels != null)
        {
            foreach (var l in extraLabels) strings.Add(l);
            if (seriesAName != null) strings.Add(seriesAName);
            if (seriesBName != null) strings.Add(seriesBName);
        }

        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append($"<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"{strings.Count}\" uniqueCount=\"{strings.Count}\">");
        foreach (var s in strings)
        {
            sb.Append("<si><t xml:space=\"preserve\">");
            sb.Append(XmlEscape(s));
            sb.Append("</t></si>");
        }
        sb.Append("</sst>");
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Main data sheet
    // -------------------------------------------------------------------------

    private static string SheetXml(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<object?>> rows,
        string reportTitle,
        string generatedBy,
        int colCount,
        IReadOnlyList<string>? columnFormats)
    {
        // Row layout:
        // 1: Brand banner "EQUAMERIDIAN HOLDINGS"
        // 2: Report title
        // 3: Generated meta
        // 4: blank
        // 5: column headers
        // 6+: data
        int headerRow = 5;
        int firstDataRow = 6;
        int lastDataRow = firstDataRow + Math.Max(rows.Count, 1) - 1;
        if (rows.Count == 0) lastDataRow = firstDataRow;

        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ");
        sb.Append("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");

        // Freeze header row (must appear before sheetData in OOXML)
        sb.Append($"<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"{headerRow}\" topLeftCell=\"A{headerRow + 1}\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");

        // Column widths — generous defaults; currency cols a bit wider
        sb.Append("<cols>");
        for (int c = 0; c < colCount; c++)
        {
            double width = 16;
            if (c == 0) width = 22;
            if (columnFormats != null && c < columnFormats.Count && columnFormats[c] == "currency")
                width = 14;
            sb.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{width.ToString(CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
        }
        sb.Append("</cols>");

        sb.Append("<sheetData>");

        // Row 1 — brand banner (merged across columns)
        sb.Append($"<row r=\"1\" ht=\"28\" customHeight=\"1\">");
        sb.Append($"<c r=\"A1\" t=\"s\" s=\"1\"><v>0</v></c>");
        for (int c = 1; c < colCount; c++)
            sb.Append($"<c r=\"{Col(c + 1)}1\" s=\"1\"/>");
        sb.Append("</row>");

        // Row 2 — report title
        sb.Append($"<row r=\"2\" ht=\"20\" customHeight=\"1\">");
        sb.Append($"<c r=\"A2\" t=\"s\" s=\"1\"><v>1</v></c>");
        for (int c = 1; c < colCount; c++)
            sb.Append($"<c r=\"{Col(c + 1)}2\" s=\"1\"/>");
        sb.Append("</row>");

        // Row 3 — meta
        sb.Append($"<row r=\"3\">");
        sb.Append($"<c r=\"A3\" t=\"s\" s=\"2\"><v>2</v></c>");
        for (int c = 1; c < colCount; c++)
            sb.Append($"<c r=\"{Col(c + 1)}3\" s=\"2\"/>");
        sb.Append("</row>");

        // Row 4 blank
        sb.Append("<row r=\"4\"/>");

        // Row 5 — column headers
        sb.Append($"<row r=\"{headerRow}\" ht=\"22\" customHeight=\"1\">");
        for (int c = 0; c < headers.Count; c++)
        {
            var cellRef = $"{Col(c + 1)}{headerRow}";
            sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\" s=\"3\"><is><t xml:space=\"preserve\">{XmlEscape(headers[c])}</t></is></c>");
        }
        sb.Append("</row>");

        // Data rows
        for (int r = 0; r < rows.Count; r++)
        {
            int rowNum = firstDataRow + r;
            var row = rows[r];
            bool isAlt = r % 2 == 1;
            bool isSubtotal = row.Count > 1 && row[1]?.ToString()?.Equals("SUBTOTAL", StringComparison.OrdinalIgnoreCase) == true;
            bool isGrand = row.Count > 0 && row[0]?.ToString()?.Equals("GRAND TOTAL", StringComparison.OrdinalIgnoreCase) == true;

            sb.Append($"<row r=\"{rowNum}\">");
            for (int c = 0; c < Math.Max(row.Count, headers.Count); c++)
            {
                var value = c < row.Count ? row[c] : null;
                var cellRef = $"{Col(c + 1)}{rowNum}";
                string fmt = columnFormats != null && c < columnFormats.Count ? columnFormats[c] : InferFormat(value, headers, c);

                int style;
                if (isGrand) style = fmt == "currency" ? 13 : 9;
                else if (isSubtotal) style = fmt == "currency" ? 12 : 8;
                else style = StyleFor(fmt, isAlt);

                AppendCell(sb, cellRef, value, style, fmt);
            }
            sb.Append("</row>");
        }

        if (rows.Count == 0)
        {
            sb.Append($"<row r=\"{firstDataRow}\">");
            sb.Append($"<c r=\"A{firstDataRow}\" t=\"inlineStr\" s=\"4\"><is><t>No records match the current filters.</t></is></c>");
            sb.Append("</row>");
        }

        sb.Append("</sheetData>");

        // Merge banner cells
        if (colCount > 1)
        {
            sb.Append("<mergeCells count=\"3\">");
            sb.Append($"<mergeCell ref=\"A1:{Col(colCount)}1\"/>");
            sb.Append($"<mergeCell ref=\"A2:{Col(colCount)}2\"/>");
            sb.Append($"<mergeCell ref=\"A3:{Col(colCount)}3\"/>");
            sb.Append("</mergeCells>");
        }

        // AutoFilter on header row
        sb.Append($"<autoFilter ref=\"A{headerRow}:{Col(colCount)}{lastDataRow}\"/>");

        // Print setup — landscape, branded header/footer
        sb.Append("<pageMargins left=\"0.5\" right=\"0.5\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");
        sb.Append("<pageSetup orientation=\"landscape\" fitToPage=\"1\" fitToWidth=\"1\" fitToHeight=\"0\"/>");
        sb.Append("<headerFooter><oddHeader>&amp;C&amp;\"Calibri,Bold\"&amp;12EQUAMERIDIAN HOLDINGS</oddHeader>");
        sb.Append("<oddFooter>&amp;LConfidential&amp;CPage &amp;P of &amp;N&amp;RGenerated by EquaMeridian</oddFooter></headerFooter>");

        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static int StyleFor(string fmt, bool isAlt) => fmt switch
    {
        "currency" => isAlt ? 7 : 6,
        "integer" => isAlt ? 11 : 10,
        "date" => isAlt ? 15 : 14,
        _ => isAlt ? 5 : 4
    };

    private static string InferFormat(object? value, IReadOnlyList<string> headers, int col)
    {
        var h = col < headers.Count ? headers[col].ToLowerInvariant() : "";
        if (h.Contains("rate") || h.Contains("amount") || h.Contains("fee") || h.Contains("total") ||
            h.Contains("subtotal") || h.Contains("revenue") || h.Contains("price") || h.Contains("zar") ||
            h.Contains("commission") || h.Contains("cost"))
            return "currency";
        if (h.Contains("count") || h.Contains("qty") || h.Contains("quantity") || h.Contains("days") ||
            h.Contains("id") || h.Contains("number of"))
            return "integer";
        if (h.Contains("date") || h.Contains("applied"))
            return "date";
        if (value is decimal or double or float) return "currency";
        if (value is int or long) return "integer";
        if (value is DateTime) return "date";
        return "text";
    }

    private static void AppendCell(StringBuilder sb, string cellRef, object? value, int style, string fmt)
    {
        if (value is null)
        {
            sb.Append($"<c r=\"{cellRef}\" s=\"{style}\"/>");
            return;
        }

        if (value is int or long or short or byte)
        {
            sb.Append($"<c r=\"{cellRef}\" s=\"{style}\"><v>{value}</v></c>");
            return;
        }

        if (value is decimal or double or float)
        {
            var n = Convert.ToDecimal(value).ToString(CultureInfo.InvariantCulture);
            sb.Append($"<c r=\"{cellRef}\" s=\"{style}\"><v>{n}</v></c>");
            return;
        }

        if (value is DateTime dt)
        {
            // Excel serial date
            var serial = dt.ToOADate().ToString(CultureInfo.InvariantCulture);
            sb.Append($"<c r=\"{cellRef}\" s=\"{style}\"><v>{serial}</v></c>");
            return;
        }

        if (value is bool b)
        {
            sb.Append($"<c r=\"{cellRef}\" t=\"b\" s=\"{style}\"><v>{(b ? 1 : 0)}</v></c>");
            return;
        }

        var text = XmlEscape(value.ToString() ?? "");
        sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\" s=\"{style}\"><is><t xml:space=\"preserve\">{text}</t></is></c>");
    }

    // -------------------------------------------------------------------------
    // Chart data sheet + chart XML
    // -------------------------------------------------------------------------

    private static string ChartDataSheetXml(
        IReadOnlyList<string> labels,
        IReadOnlyList<decimal> seriesA,
        IReadOnlyList<decimal> seriesB,
        string seriesAName,
        string seriesBName)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ");
        sb.Append("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
        sb.Append("<drawing r:id=\"rId1\"/>");
        sb.Append("<sheetData>");

        // Header
        sb.Append("<row r=\"1\">");
        sb.Append("<c r=\"A1\" t=\"inlineStr\" s=\"3\"><is><t>Month</t></is></c>");
        sb.Append($"<c r=\"B1\" t=\"inlineStr\" s=\"3\"><is><t>{XmlEscape(seriesAName)}</t></is></c>");
        sb.Append($"<c r=\"C1\" t=\"inlineStr\" s=\"3\"><is><t>{XmlEscape(seriesBName)}</t></is></c>");
        sb.Append("</row>");

        for (int i = 0; i < labels.Count; i++)
        {
            int r = i + 2;
            var a = i < seriesA.Count ? seriesA[i] : 0m;
            var b = i < seriesB.Count ? seriesB[i] : 0m;
            sb.Append($"<row r=\"{r}\">");
            sb.Append($"<c r=\"A{r}\" t=\"inlineStr\" s=\"4\"><is><t>{XmlEscape(labels[i])}</t></is></c>");
            sb.Append($"<c r=\"B{r}\" s=\"6\"><v>{a.ToString(CultureInfo.InvariantCulture)}</v></c>");
            sb.Append($"<c r=\"C{r}\" s=\"6\"><v>{b.ToString(CultureInfo.InvariantCulture)}</v></c>");
            sb.Append("</row>");
        }

        sb.Append("</sheetData>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static string ChartSheetRelsXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing\" Target=\"../drawings/drawing1.xml\"/>" +
        "</Relationships>";

    private static string DrawingXml() =>
        """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing"
          xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
          xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart"
          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <xdr:twoCellAnchor>
    <xdr:from><xdr:col>0</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>0</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from>
    <xdr:to><xdr:col>12</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>20</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to>
    <xdr:graphicFrame>
      <xdr:nvGraphicFramePr>
        <xdr:cNvPr id="2" name="Chart 1"/>
        <xdr:cNvGraphicFramePr/>
      </xdr:nvGraphicFramePr>
      <xdr:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/></xdr:xfrm>
      <a:graphic>
        <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart">
          <c:chart r:id="rId1"/>
        </a:graphicData>
      </a:graphic>
    </xdr:graphicFrame>
    <xdr:clientData/>
  </xdr:twoCellAnchor>
</xdr:wsDr>
""";

    private static string DrawingRelsXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart\" Target=\"../charts/chart1.xml\"/>" +
        "</Relationships>";

    private static string BarChartXml(string title, int pointCount, string seriesAName, string seriesBName)
    {
        // Categories in A2:A{n+1}, Series A in B2:B{n+1}, Series B in C2:C{n+1} on sheet Chart (sheet2)
        int lastRow = pointCount + 1;
        string catRef = $"'Chart'!$A$2:$A${lastRow}";
        string valARef = $"'Chart'!$B$2:$B${lastRow}";
        string valBRef = $"'Chart'!$C$2:$C${lastRow}";

        return $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart"
              xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
              xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <c:chart>
    <c:title>
      <c:tx><c:rich><a:bodyPr/><a:lstStyle/>
        <a:p><a:pPr><a:defRPr sz="1400" b="1"/></a:pPr>
          <a:r><a:rPr lang="en-US" sz="1400" b="1"/><a:t>{XmlEscape(title)}</a:t></a:r>
        </a:p>
      </c:rich></c:tx>
      <c:overlay val="0"/>
    </c:title>
    <c:autoTitleDeleted val="0"/>
    <c:plotArea>
      <c:layout/>
      <c:barChart>
        <c:barDir val="col"/>
        <c:grouping val="clustered"/>
        <c:varyColors val="0"/>
        <c:ser>
          <c:idx val="0"/><c:order val="0"/>
          <c:tx><c:strRef><c:f>'Chart'!$B$1</c:f></c:strRef></c:tx>
          <c:spPr><a:solidFill><a:srgbClr val="C84B11"/></a:solidFill></c:spPr>
          <c:cat><c:strRef><c:f>{catRef}</c:f></c:strRef></c:cat>
          <c:val><c:numRef><c:f>{valARef}</c:f></c:numRef></c:val>
        </c:ser>
        <c:ser>
          <c:idx val="1"/><c:order val="1"/>
          <c:tx><c:strRef><c:f>'Chart'!$C$1</c:f></c:strRef></c:tx>
          <c:spPr><a:solidFill><a:srgbClr val="2F6FED"/></a:solidFill></c:spPr>
          <c:cat><c:strRef><c:f>{catRef}</c:f></c:strRef></c:cat>
          <c:val><c:numRef><c:f>{valBRef}</c:f></c:numRef></c:val>
        </c:ser>
        <c:axId val="1"/><c:axId val="2"/>
      </c:barChart>
      <c:catAx>
        <c:axId val="1"/><c:scaling><c:orientation val="minMax"/></c:scaling>
        <c:axPos val="b"/><c:tickLblPos val="nextTo"/>
        <c:crossAx val="2"/><c:crosses val="autoZero"/>
      </c:catAx>
      <c:valAx>
        <c:axId val="2"/><c:scaling><c:orientation val="minMax"/></c:scaling>
        <c:axPos val="l"/><c:majorGridlines/>
        <c:numFmt formatCode="&quot;R&quot;#,##0" sourceLinked="0"/>
        <c:tickLblPos val="nextTo"/>
        <c:crossAx val="1"/><c:crosses val="autoZero"/>
      </c:valAx>
    </c:plotArea>
    <c:legend><c:legendPos val="b"/><c:overlay val="0"/></c:legend>
    <c:plotVisOnly val="1"/>
  </c:chart>
</c:chartSpace>
""";
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string Col(int oneBased)
    {
        var sb = new StringBuilder();
        int n = oneBased;
        while (n > 0)
        {
            n--;
            sb.Insert(0, (char)('A' + (n % 26)));
            n /= 26;
        }
        return sb.ToString();
    }

    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? ' ' : ch).ToArray()).Trim();
        if (cleaned.Length > 31) cleaned = cleaned[..31];
        return string.IsNullOrEmpty(cleaned) ? "Sheet1" : cleaned;
    }

    private static string XmlEscape(string value) =>
        System.Security.SecurityElement.Escape(value) ?? value;
}
