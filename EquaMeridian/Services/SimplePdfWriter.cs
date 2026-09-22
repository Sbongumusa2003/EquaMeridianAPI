using System.Globalization;
using System.Text;

/// <summary>
/// Lightweight PDF writer with EquaMeridian Holdings branding: black/orange masthead,
/// company name, report title, generated-by meta, page numbers, and a full bar-chart
/// report for management graphs. No third-party PDF libraries required.
/// </summary>
public static class SimplePdfWriter
{
    private const double PageWidth = 612;
    private const double PageHeight = 792;
    private const double Margin = 40;
    private const double FontSize = 8;
    private const double LineHeight = 12;
    private const double TitleFontSize = 13;
    private const double LogoFontSize = 15;
    private const double MetaFontSize = 8;

    // Brand RGB (0–1) — EquaMeridian gold #C9A227
    private static readonly double BrandR = 201 / 255.0;
    private static readonly double BrandG = 162 / 255.0;
    private static readonly double BrandB = 39 / 255.0;
    private static readonly double CharcoalR = 14 / 255.0;
    private static readonly double CharcoalG = 13 / 255.0;
    private static readonly double CharcoalB = 11 / 255.0;

    public static byte[] Generate(string title, IEnumerable<string> lines, string generatedBy = "EquaMeridian Admin Portal")
    {
        var allLines = lines.ToList();
        var headerHeight = LogoFontSize + 4 + TitleFontSize + 4 + MetaFontSize + 16 + 8; // + accent bar
        var usableHeight = PageHeight - (2 * Margin) - headerHeight - 20; // footer space
        var linesPerPage = Math.Max(1, (int)(usableHeight / LineHeight));

        var pagesOfLines = new List<List<string>>();
        for (int i = 0; i < allLines.Count; i += linesPerPage)
            pagesOfLines.Add(allLines.Skip(i).Take(linesPerPage).ToList());

        if (pagesOfLines.Count == 0)
            pagesOfLines.Add(new List<string> { "No records match the current search or filter criteria." });

        var generatedAt = AppTime.Now;

        var objects = new List<string>
        {
            string.Empty,
            string.Empty,
            string.Empty,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"
        };

        // Object layout: 1=Catalog, 2=Pages, 3=Helvetica, 4=Helvetica-Bold,
        // then for each page: content stream then page dict (ids must match list indices).
        var pageObjIds = new List<int>();
        var contentObjIds = new List<int>();

        for (int pageIndex = 0; pageIndex < pagesOfLines.Count; pageIndex++)
        {
            var stream = BuildContentStream(title, generatedBy, generatedAt, pagesOfLines[pageIndex],
                pageIndex + 1, pagesOfLines.Count);
            int contentId = objects.Count; // next index
            objects.Add($"<< /Length {Encoding.Latin1.GetByteCount(stream)} >>\nstream\n{stream}\nendstream");
            contentObjIds.Add(contentId);

            int pageId = objects.Count;
            objects.Add(
                $"<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 3 0 R /F1B 4 0 R >> >> " +
                $"/MediaBox [0 0 {PageWidth} {PageHeight}] /Contents {contentId} 0 R >>");
            pageObjIds.Add(pageId);
        }

        var kids = string.Join(" ", pageObjIds.Select(id => $"{id} 0 R"));
        objects[1] = "<< /Type /Catalog /Pages 2 0 R >>";
        objects[2] = $"<< /Type /Pages /Kids [{kids}] /Count {pageObjIds.Count} >>";

        return AssemblePdf(objects);
    }

    private static string BuildContentStream(
        string title, string generatedBy, DateTime generatedAt, List<string> lines, int pageNumber, int totalPages)
    {
        var sb = new StringBuilder();
        double y = PageHeight - Margin;

        // Dual top accent: charcoal band + gold rule
        sb.Append($"{Fmt(CharcoalR)} {Fmt(CharcoalG)} {Fmt(CharcoalB)} rg\n");
        sb.Append($"{Fmt(0)} {Fmt(PageHeight - 10)} {Fmt(PageWidth)} {Fmt(10)} re\nf\n");
        sb.Append($"{Fmt(BrandR)} {Fmt(BrandG)} {Fmt(BrandB)} rg\n");
        sb.Append($"{Fmt(0)} {Fmt(PageHeight - 13)} {Fmt(PageWidth)} {Fmt(3)} re\nf\n");
        y = PageHeight - Margin - 8;

        // Company name (charcoal + gold HOLDINGS)
        sb.Append("BT\n");
        sb.Append($"/F1B {LogoFontSize.ToString(CultureInfo.InvariantCulture)} Tf\n");
        sb.Append($"{Fmt(CharcoalR)} {Fmt(CharcoalG)} {Fmt(CharcoalB)} rg\n");
        sb.Append($"{Margin} {y.ToString(CultureInfo.InvariantCulture)} Td\n");
        sb.Append("(EQUAMERIDIAN ) Tj\n");
        sb.Append($"{Fmt(BrandR)} {Fmt(BrandG)} {Fmt(BrandB)} rg\n");
        sb.Append("(HOLDINGS) Tj\n");
        sb.Append("ET\n");
        y -= LogoFontSize + 2;

        sb.Append("BT\n");
        sb.Append($"/F1 7 Tf\n0.45 0.43 0.40 rg\n");
        sb.Append($"{Margin} {y.ToString(CultureInfo.InvariantCulture)} Td\n");
        sb.Append("(Machinery Marketplace) Tj\n");
        sb.Append("ET\n");
        y -= 14;

        // Report title
        sb.Append("BT\n");
        sb.Append($"/F1B {TitleFontSize.ToString(CultureInfo.InvariantCulture)} Tf\n");
        sb.Append($"{Fmt(CharcoalR)} {Fmt(CharcoalG)} {Fmt(CharcoalB)} rg\n");
        sb.Append($"{Margin} {y.ToString(CultureInfo.InvariantCulture)} Td\n");
        sb.Append($"({Escape(title)}) Tj\n");
        sb.Append("ET\n");
        y -= TitleFontSize + 5;

        // Meta line
        sb.Append("BT\n");
        sb.Append($"/F1 {MetaFontSize.ToString(CultureInfo.InvariantCulture)} Tf\n");
        sb.Append($"0.4 0.38 0.35 rg\n");
        sb.Append($"{Margin} {y.ToString(CultureInfo.InvariantCulture)} Td\n");
        sb.Append($"(Generated by {Escape(generatedBy)}   -   {generatedAt:dd MMM yyyy, HH:mm} SAST   -   Page {pageNumber} of {totalPages}) Tj\n");
        sb.Append("ET\n");
        y -= MetaFontSize + 8;

        // Double rule
        sb.Append($"{Fmt(BrandR)} {Fmt(BrandG)} {Fmt(BrandB)} RG\n1.2 w\n");
        sb.Append($"{Fmt(Margin)} {Fmt(y)} m\n{Fmt(PageWidth - Margin)} {Fmt(y)} l\nS\n");
        sb.Append($"{Fmt(CharcoalR)} {Fmt(CharcoalG)} {Fmt(CharcoalB)} RG\n0.4 w\n");
        sb.Append($"{Fmt(Margin)} {Fmt(y - 2.5)} m\n{Fmt(PageWidth - Margin)} {Fmt(y - 2.5)} l\nS\n");
        y -= 14;

        // Body
        sb.Append("BT\n");
        sb.Append($"/F1 {FontSize.ToString(CultureInfo.InvariantCulture)} Tf\n");
        sb.Append($"0 0 0 rg\n");
        sb.Append($"{LineHeight.ToString(CultureInfo.InvariantCulture)} TL\n");
        sb.Append($"{Margin} {y.ToString(CultureInfo.InvariantCulture)} Td\n");

        foreach (var line in lines)
        {
            sb.Append($"({Escape(line)}) Tj\n");
            sb.Append("T*\n");
        }
        sb.Append("ET\n");

        // Footer bar
        sb.Append($"{Fmt(CharcoalR)} {Fmt(CharcoalG)} {Fmt(CharcoalB)} rg\n");
        sb.Append($"0 0 {Fmt(PageWidth)} 20 re\nf\n");
        sb.Append($"{Fmt(BrandR)} {Fmt(BrandG)} {Fmt(BrandB)} rg\n");
        sb.Append($"0 20 {Fmt(PageWidth)} 2 re\nf\n");
        sb.Append("BT\n");
        sb.Append($"/F1 {MetaFontSize.ToString(CultureInfo.InvariantCulture)} Tf\n");
        sb.Append($"0.85 0.82 0.75 rg\n");
        sb.Append($"{Margin} 7 Td\n");
        sb.Append("(CONFIDENTIAL  -  EquaMeridian Holdings  -  Machinery Marketplace) Tj\n");
        sb.Append("ET");

        return sb.ToString();
    }

    /// <summary>Renders an actual bar chart (axes, scaled bars, legend, gridlines) into a PDF —
    /// not a text table pretending to be a report. Two series (e.g. Revenue vs Commission) are
    /// drawn as paired bars per month, plus a compact figures table underneath for exact values.
    /// Single page: callers should cap the series to a sensible window (e.g. last 12 months).</summary>
    public static byte[] GenerateBarChartReport(
        string title,
        List<string> monthLabels,
        List<decimal> seriesA, string seriesAName, string seriesAColorHex,
        List<decimal> seriesB, string seriesBName, string seriesBColorHex,
        List<string> tableRows,
        string generatedBy = "EquaMeridian Admin Portal")
    {
        var generatedAt = AppTime.Now;
        var (rA, gA, bA) = HexToRgb(seriesAColorHex);
        var (rB, gB, bB) = HexToRgb(seriesBColorHex);

        double y = PageHeight - Margin;
        var sb = new StringBuilder();

        // Top accent bar
        sb.Append($"{Fmt(BrandR)} {Fmt(BrandG)} {Fmt(BrandB)} rg\n");
        sb.Append($"{Fmt(0)} {Fmt(PageHeight - 6)} {Fmt(PageWidth)} {Fmt(6)} re\nf\n");

        // Header
        sb.Append("BT\n").Append($"/F1B {LogoFontSize.ToString(CultureInfo.InvariantCulture)} Tf\n")
          .Append($"0 0 0 rg\n")
          .Append($"{Margin} {y.ToString(CultureInfo.InvariantCulture)} Td\n(EQUAMERIDIAN HOLDINGS) Tj\nET\n");
        y -= LogoFontSize + 4;

        sb.Append($"{Fmt(BrandR)} {Fmt(BrandG)} {Fmt(BrandB)} RG\n1.5 w\n");
        sb.Append($"{Fmt(Margin)} {Fmt(y + 2)} m\n{Fmt(Margin + 180)} {Fmt(y + 2)} l\nS\n");
        y -= 6;

        sb.Append("BT\n").Append($"/F1B {TitleFontSize.ToString(CultureInfo.InvariantCulture)} Tf\n")
          .Append($"0 0 0 rg\n")
          .Append($"{Margin} {y.ToString(CultureInfo.InvariantCulture)} Td\n({Escape(title)}) Tj\nET\n");
        y -= TitleFontSize + 4;

        sb.Append("BT\n").Append($"/F1 {MetaFontSize.ToString(CultureInfo.InvariantCulture)} Tf\n")
          .Append($"0.4 0.4 0.4 rg\n")
          .Append($"{Margin} {y.ToString(CultureInfo.InvariantCulture)} Td\n")
          .Append($"(Generated: {generatedAt:yyyy-MM-dd HH:mm} SAST   |   Generated by: {Escape(generatedBy)}) Tj\nET\n");
        y -= MetaFontSize + 14;

        // Legend
        double legendY = y;
        DrawSwatch(sb, Margin, legendY, rA, gA, bA);
        DrawLabel(sb, Margin + 14, legendY + 1, seriesAName);
        DrawSwatch(sb, Margin + 180, legendY, rB, gB, bB);
        DrawLabel(sb, Margin + 194, legendY + 1, seriesBName);
        y -= 22;

        // Chart area
        double chartTop = y;
        double chartHeight = 220;
        double chartBottom = chartTop - chartHeight;
        double chartLeft = Margin + 44;
        double chartRight = PageWidth - Margin;
        double chartWidth = chartRight - chartLeft;

        decimal maxValue = Math.Max(
            seriesA.DefaultIfEmpty(0).Max(),
            seriesB.DefaultIfEmpty(0).Max());
        if (maxValue <= 0) maxValue = 1;
        decimal axisMax = RoundUpToNiceNumber(maxValue);

        // Axes
        DrawLine(sb, chartLeft, chartBottom, chartRight, chartBottom, 0.55, 0.55, 0.55);
        DrawLine(sb, chartLeft, chartBottom, chartLeft, chartTop, 0.55, 0.55, 0.55);

        // Gridlines + Y labels
        for (int g = 0; g <= 4; g++)
        {
            double gy = chartBottom + (chartHeight * g / 4.0);
            DrawLine(sb, chartLeft, gy, chartRight, gy, 0.9, 0.9, 0.9);
            var value = axisMax * g / 4;
            DrawLabel(sb, Margin, gy - 3, "R" + value.ToString("N0", CultureInfo.InvariantCulture), 7);
        }

        // Bars
        int n = monthLabels.Count;
        if (n > 0)
        {
            double slotWidth = chartWidth / n;
            double barWidth = Math.Min(18, slotWidth * 0.32);
            for (int i = 0; i < n; i++)
            {
                double slotCenter = chartLeft + slotWidth * i + slotWidth / 2;
                double aHeight = (double)(seriesA[i] / axisMax) * chartHeight;
                double bHeight = (double)(seriesB[i] / axisMax) * chartHeight;

                DrawRect(sb, slotCenter - barWidth - 2, chartBottom, barWidth, aHeight, rA, gA, bA);
                DrawRect(sb, slotCenter + 2, chartBottom, barWidth, bHeight, rB, gB, bB);

                DrawLabel(sb, slotCenter - (slotWidth * 0.35), chartBottom - 12, monthLabels[i], 6.5);
            }
        }

        y = chartBottom - 28;

        // Figures table
        sb.Append("BT\n").Append($"/F1B {FontSize.ToString(CultureInfo.InvariantCulture)} Tf\n")
          .Append($"0 0 0 rg\n")
          .Append($"{Margin} {y.ToString(CultureInfo.InvariantCulture)} Td\n(Monthly Figures) Tj\nET\n");
        y -= LineHeight + 2;

        sb.Append("BT\n").Append($"/F1 {FontSize.ToString(CultureInfo.InvariantCulture)} Tf\n")
          .Append($"{LineHeight.ToString(CultureInfo.InvariantCulture)} TL\n")
          .Append($"{Margin} {y.ToString(CultureInfo.InvariantCulture)} Td\n");
        foreach (var line in tableRows)
        {
            sb.Append($"({Escape(line)}) Tj\nT*\n");
        }
        sb.Append("ET\n");

        // Footer
        sb.Append("BT\n").Append($"/F1 {MetaFontSize.ToString(CultureInfo.InvariantCulture)} Tf\n")
          .Append($"0.5 0.5 0.5 rg\n")
          .Append($"{Margin} {Fmt(Margin - 12)} Td\n")
          .Append("(Confidential  |  EquaMeridian Holdings  |  Machinery Marketplace) Tj\nET");

        var objects = new List<string>
        {
            string.Empty,
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [5 0 R] /Count 1 >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>",
        };
        var stream = sb.ToString();
        objects.Add($"<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 3 0 R /F1B 4 0 R >> >> " +
                    $"/MediaBox [0 0 {PageWidth} {PageHeight}] /Contents 6 0 R >>");
        objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream");

        return AssemblePdf(objects);
    }

    private static void DrawRect(StringBuilder sb, double x, double y, double width, double height, double r, double g, double b)
    {
        if (height < 0.5) height = 0.5;
        sb.Append($"{Fmt(r)} {Fmt(g)} {Fmt(b)} rg\n{Fmt(x)} {Fmt(y)} {Fmt(width)} {Fmt(height)} re\nf\n");
    }

    private static void DrawSwatch(StringBuilder sb, double x, double y, double r, double g, double b)
        => DrawRect(sb, x, y, 10, 10, r, g, b);

    private static void DrawLine(StringBuilder sb, double x1, double y1, double x2, double y2, double r, double g, double b)
    {
        sb.Append($"{Fmt(r)} {Fmt(g)} {Fmt(b)} RG\n0.6 w\n{Fmt(x1)} {Fmt(y1)} m\n{Fmt(x2)} {Fmt(y2)} l\nS\n");
    }

    private static void DrawLabel(StringBuilder sb, double x, double y, string text, double fontSize = 8)
    {
        sb.Append("BT\n").Append($"/F1 {fontSize.ToString(CultureInfo.InvariantCulture)} Tf\n")
          .Append($"0 0 0 rg\n")
          .Append($"{Fmt(x)} {Fmt(y)} Td\n({Escape(text)}) Tj\nET\n");
    }

    private static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static (double r, double g, double b) HexToRgb(string hex)
    {
        hex = hex.TrimStart('#');
        int r = Convert.ToInt32(hex.Substring(0, 2), 16);
        int g = Convert.ToInt32(hex.Substring(2, 2), 16);
        int b = Convert.ToInt32(hex.Substring(4, 2), 16);
        return (r / 255.0, g / 255.0, b / 255.0);
    }

    private static decimal RoundUpToNiceNumber(decimal value)
    {
        if (value <= 0) return 1;
        decimal magnitude = 1;
        while (magnitude * 10 <= value) magnitude *= 10;
        var steps = new[] { 1m, 2m, 2.5m, 5m, 10m };
        foreach (var step in steps)
        {
            var candidate = magnitude * step;
            if (candidate >= value) return candidate;
        }
        return magnitude * 10;
    }

    private static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        // PDF literal strings are Latin-1; strip chars that would break the stream
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch == '\\') sb.Append("\\\\");
            else if (ch == '(') sb.Append("\\(");
            else if (ch == ')') sb.Append("\\)");
            else if (ch > 255) sb.Append('?');
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    private static byte[] AssemblePdf(List<string> objects)
    {
        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");

        var offsets = new List<int>();
        for (int i = 1; i < objects.Count; i++)
        {
            offsets.Add(Encoding.Latin1.GetByteCount(sb.ToString()));
            sb.Append($"{i} 0 obj\n{objects[i]}\nendobj\n");
        }

        int xrefStart = Encoding.Latin1.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objects.Count}\n");
        sb.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
            sb.Append($"{offset:D10} 00000 n \n");

        sb.Append("trailer\n");
        sb.Append($"<< /Size {objects.Count} /Root 1 0 R >>\n");
        sb.Append("startxref\n");
        sb.Append(xrefStart);
        sb.Append("\n%%EOF");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }
}
