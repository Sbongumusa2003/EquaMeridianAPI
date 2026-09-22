using System.Globalization;
using System.Reflection;
using System.Text;

public sealed class PdfKpi
{
    public string Label { get; }
    public string Value { get; }
    public PdfKpi(string label, string value) { Label = label; Value = value; }
}

public enum PdfRowStyle { Normal, GroupHeader, Subtotal, GrandTotal }

public sealed class PdfTableRow
{
    public string[] Cells { get; }
    public PdfRowStyle Style { get; }
    public PdfTableRow(string[] cells, PdfRowStyle style = PdfRowStyle.Normal)
    {
        Cells = cells;
        Style = style;
    }
}

public sealed class PdfChartSeries
{
    public string Name { get; }
    public string ColorHex { get; }
    public List<decimal> Values { get; }
    public PdfChartSeries(string name, List<decimal> values, string? colorHex = null)
    {
        Name = name;
        Values = values;
        ColorHex = colorHex ?? "";
    }
}

public sealed class PdfReportEngine
{
    // ---- Page geometry ----
    private const double PageWidth = 612;
    private const double PageHeight = 792;
    private const double Margin = 42;
    private const double ContentTop = 648;
    private const double ContentBottom = 72;
    private const double ContentWidth = PageWidth - (2 * Margin);
    private const double BlockGap = 14;

    // ---- Brand palette (aligned with EquaMeridian Hub UI: gold #C9A227, charcoal #0E0D0B) ----
    private const string BrandGoldHex = "C9A227";
    private const string BrandGoldDeepHex = "A8841A";
    private const string BrandGoldSoftHex = "E0C36A";
    private const string BrandCharcoalHex = "0E0D0B";
    private const string BrandInkHex = "1A1814";
    private const string BrandSandHex = "F4F0E8";
    private const string BrandMutedHex = "9A958C";
    private static readonly string[] ChartPalette = { "C9A227", "0E0D0B", "A8841A", "E0C36A", "5C574E", "8B6914" };

    // ---- Embedded logo (loaded once from the assembly's embedded resources) ----
    private static readonly byte[]? LogoBytes = LoadLogoBytes();
    private const int LogoPxWidth = 472;
    private const int LogoPxHeight = 472;

    private readonly string _title;
    private readonly string? _subtitle;
    private readonly string _generatedBy;
    private readonly DateTime _generatedAt = AppTime.Now;

    private readonly List<string> _pages = new();
    private StringBuilder _current = new();
    private double _cursorY;
    private int _pageNum;

    public PdfReportEngine(string title, string? subtitle, string generatedBy)
    {
        _title = title;
        _subtitle = subtitle;
        _generatedBy = generatedBy;
        BeginPage();
    }

    // =====================================================================
    // Public block API
    // =====================================================================

    public PdfReportEngine AddKpis(params PdfKpi[] kpis)
    {
        if (kpis.Length == 0) return this;
        const double height = 62;
        EnsureSpace(height);

        double gap = 8;
        double cardWidth = (ContentWidth - (gap * (kpis.Length - 1))) / kpis.Length;
        var (gr, gg, gb) = HexToRgb(BrandGoldHex);
        var (cr, cg, cb) = HexToRgb(BrandCharcoalHex);
        var (sr, sg, sb) = HexToRgb(BrandSandHex);

        for (int i = 0; i < kpis.Length; i++)
        {
            double x = Margin + (i * (cardWidth + gap));
            double w = cardWidth;

            // Card fill + soft border via inset sand frame
            DrawRect(_current, x, _cursorY - height, w, height, sr, sg, sb);
            DrawRect(_current, x + 0.6, _cursorY - height + 0.6, w - 1.2, height - 1.2, 1, 1, 1);
            // Gold top accent
            DrawRect(_current, x, _cursorY - 3, w, 3, gr, gg, gb);
            // Thin left charcoal rail for hierarchy
            DrawRect(_current, x, _cursorY - height, 2.5, height, cr, cg, cb);

            DrawText(_current, x + 12, _cursorY - 18, kpis[i].Label.ToUpperInvariant(), 6.5, bold: false, 0.45, 0.43, 0.40);
            // Scale value font so full amounts (e.g. R845,834.20) fit without ellipsis
            double valueMaxW = w - 20;
            double valueSize = 15;
            string valueText = kpis[i].Value ?? "";
            while (valueSize > 9 && TextWidth(valueText, valueSize, true) > valueMaxW)
                valueSize -= 0.5;
            if (TextWidth(valueText, valueSize, true) > valueMaxW)
                valueText = Truncate(valueText, valueMaxW, valueSize, true);
            DrawText(_current, x + 12, _cursorY - 42, valueText, valueSize, bold: true, cr, cg, cb);
        }

        _cursorY -= height + BlockGap;
        return this;
    }

    public PdfReportEngine AddSectionTitle(string text)
    {
        const double height = 22;
        EnsureSpace(height);
        var (gr, gg, gb) = HexToRgb(BrandGoldHex);
        var (cr, cg, cb) = HexToRgb(BrandCharcoalHex);

        // Gold tick mark
        DrawRect(_current, Margin, _cursorY - 14, 3.5, 12, gr, gg, gb);
        DrawText(_current, Margin + 10, _cursorY - 12, text, 11, bold: true, cr, cg, cb);
        // Soft rule under the title
        DrawLine(_current, Margin + 10, _cursorY - 18, PageWidth - Margin, _cursorY - 18, gr, gg, gb, 0.9);
        _cursorY -= height + 6;
        return this;
    }

    /// <summary>Highlighted insight / narrative callout — used to surface the key takeaway
    /// so the report is not just a dump of numbers.</summary>
    public PdfReportEngine AddInsight(string title, string body)
    {
        var lines = WrapText(body, ContentWidth - 32, 8.5, false);
        double height = 24 + (lines.Count * 12) + 12;
        EnsureSpace(height);

        var (gr, gg, gb) = HexToRgb(BrandGoldHex);
        var (sr, sg, sb) = HexToRgb(BrandSandHex);
        var (dr, dg, db) = HexToRgb(BrandGoldDeepHex);

        // Sand panel + gold left rail
        DrawRect(_current, Margin, _cursorY - height, ContentWidth, height, sr, sg, sb);
        DrawRect(_current, Margin, _cursorY - height, 4, height, gr, gg, gb);
        // Top hairline
        DrawLine(_current, Margin + 4, _cursorY, PageWidth - Margin, _cursorY, gr, gg, gb, 0.5);

        DrawText(_current, Margin + 14, _cursorY - 15, title.ToUpperInvariant(), 7.2, bold: true, dr, dg, db);
        double y = _cursorY - 30;
        foreach (var line in lines)
        {
            DrawText(_current, Margin + 14, y, line, 8.5, bold: false, 0.16, 0.15, 0.13);
            y -= 12;
        }

        _cursorY -= height + BlockGap;
        return this;
    }

    /// <summary>Compact footnote / methodology note under a section.</summary>
    public PdfReportEngine AddNote(string text)
    {
        var lines = WrapText(text, ContentWidth, 7.5, false);
        double height = lines.Count * 10 + 4;
        EnsureSpace(height);
        double y = _cursorY - 8;
        foreach (var line in lines)
        {
            DrawTextOblique(_current, Margin, y, line, 7.5, 0.45, 0.45, 0.45);
            y -= 10;
        }
        _cursorY -= height + 4;
        return this;
    }

    /// <summary>Horizontal ranking bars — clearer than vertical bars when category labels are long
    /// (supplier names, dispute reasons, etc.).</summary>
    public PdfReportEngine AddHorizontalBarChart(string chartTitle, List<string> labels, List<decimal> values,
        string valuePrefix = "R", int maxBars = 8)
    {
        if (labels.Count == 0 || values.Count == 0) return this;

        int n = Math.Min(Math.Min(labels.Count, values.Count), maxBars);
        const double rowH = 20;
        const double labelCol = 132;
        const double valueCol = 72;
        double chartW = ContentWidth - labelCol - valueCol - 10;
        double blockHeight = 26 + (n * rowH) + 10;
        EnsureSpace(blockHeight);

        var (cr, cg, cb) = HexToRgb(BrandCharcoalHex);
        var (gr, gg, gb) = HexToRgb(BrandGoldHex);
        var (sr, sg, sb) = HexToRgb(BrandSandHex);

        // Section tick + title
        DrawRect(_current, Margin, _cursorY - 14, 3.5, 12, gr, gg, gb);
        DrawText(_current, Margin + 10, _cursorY - 12, chartTitle, 10.5, bold: true, cr, cg, cb);
        _cursorY -= 22;

        decimal maxVal = values.Take(n).DefaultIfEmpty(0).Max();
        if (maxVal <= 0) maxVal = 1;

        for (int i = 0; i < n; i++)
        {
            double yTop = _cursorY;
            // Track background
            DrawRect(_current, Margin + labelCol, yTop - 15, chartW, 11, sr, sg, sb);

            string label = Truncate(labels[i], labelCol - 6, 8, false);
            DrawText(_current, Margin, yTop - 12, label, 8, bold: false, 0.18, 0.17, 0.15);

            double barW = Math.Max(3, (double)(values[i] / maxVal) * chartW);
            DrawRect(_current, Margin + labelCol, yTop - 15, barW, 11, gr, gg, gb);
            // Deep-gold tip for depth
            if (barW > 6)
            {
                var (dr, dg, db) = HexToRgb(BrandGoldDeepHex);
                DrawRect(_current, Margin + labelCol + barW - 3, yTop - 15, 3, 11, dr, dg, db);
            }

            string valText = valuePrefix == "R"
                ? CompactMoney(values[i])
                : valuePrefix + values[i].ToString("N0", CultureInfo.InvariantCulture);
            DrawTextRightAligned(_current, PageWidth - Margin, yTop - 12, valText, 8, bold: true, cr, cg, cb);
            _cursorY -= rowH;
        }

        _cursorY -= 8;
        return this;
    }

    /// <summary>Donut / share chart — mirrors the on-screen "type=donut" chart (revenue share by
    /// category, booking share by category, category mix, revenue concentration). Collapses long
    /// tails into an "Other" slice beyond maxSlices, same as the Angular supplier-concentration donut.</summary>
    public PdfReportEngine AddDonutChart(string chartTitle, string? subtitle, List<string> labels, List<decimal> values,
        string valuePrefix = "R", int maxSlices = 8)
    {
        var items = labels.Zip(values, (l, v) => (Label: l, Value: v))
            .Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value)
            .ToList();
        if (items.Count == 0) return this;

        List<(string Label, decimal Value)> slices;
        if (items.Count > maxSlices)
        {
            slices = items.Take(maxSlices - 1).ToList();
            decimal other = items.Skip(maxSlices - 1).Sum(x => x.Value);
            slices.Add(($"Other ({items.Count - (maxSlices - 1)})", other));
        }
        else slices = items;

        decimal total = slices.Sum(x => x.Value);
        if (total <= 0) return this;

        int legendRows = slices.Count;
        double blockHeight = Math.Max(176, 44 + (legendRows * 15.5));
        EnsureSpace(blockHeight);

        var (dcr, dcg, dcb) = HexToRgb(BrandCharcoalHex);
        var (dgr, dgg, dgb) = HexToRgb(BrandGoldHex);
        DrawRect(_current, Margin, _cursorY - 14, 3.5, 12, dgr, dgg, dgb);
        DrawText(_current, Margin + 10, _cursorY - 12, chartTitle, 11, bold: true, dcr, dcg, dcb);
        double topY = _cursorY - 12;
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            DrawTextOblique(_current, Margin + 10, _cursorY - 24, subtitle!, 7.8, 0.42, 0.40, 0.37);
            topY -= 12;
        }

        double outerR = 58;
        double innerR = 30;
        double cx = Margin + outerR + 6;
        double cy = topY - outerR - 12;

        double angle = 90; // 12 o'clock, sweeps clockwise
        for (int i = 0; i < slices.Count; i++)
        {
            double sweep = (double)(slices[i].Value / total) * 360.0;
            string hex = ChartPalette[i % ChartPalette.Length];
            var (r, g, b) = HexToRgb(hex);
            DrawDonutSlice(_current, cx, cy, outerR, innerR, angle, sweep, r, g, b);
            angle -= sweep;
        }

        // Centre label: number of slices / total
        DrawTextCentered(_current, cx, cy - 3, valuePrefix == "R" ? CompactMoney(total) : total.ToString("N0", CultureInfo.InvariantCulture), 9, 0.10, 0.10, 0.10);
        DrawTextCentered(_current, cx, cy - 14, "TOTAL", 6, 0.5, 0.5, 0.5);

        double legendX = cx + outerR + 26;
        double legendY = topY - 6;
        double maxLegendWidth = (PageWidth - Margin) - legendX;
        for (int i = 0; i < slices.Count; i++)
        {
            string hex = ChartPalette[i % ChartPalette.Length];
            var (r, g, b) = HexToRgb(hex);
            DrawRect(_current, legendX, legendY - 8, 8, 8, r, g, b);
            string pct = (slices[i].Value / total * 100m).ToString("0.#", CultureInfo.InvariantCulture) + "%";
            string valText = valuePrefix == "R" ? CompactMoney(slices[i].Value)
                : valuePrefix + slices[i].Value.ToString("N0", CultureInfo.InvariantCulture);
            string label = Truncate(slices[i].Label, maxLegendWidth - 16, 8, false);
            DrawText(_current, legendX + 13, legendY - 7, label, 8, bold: false, 0.15, 0.15, 0.15);
            DrawTextRightAligned(_current, PageWidth - Margin, legendY - 7, $"{pct} \u00b7 {valText}", 7.5, bold: false, 0.4, 0.4, 0.4);
            legendY -= 15.5;
        }

        _cursorY -= blockHeight;
        return this;
    }

    public PdfReportEngine AddBarChart(string chartTitle, List<string> categories, List<PdfChartSeries> series,
        string valuePrefix = "R", string valueSuffix = "")
    {
        DrawChart(chartTitle, categories, series, valuePrefix, valueSuffix, isLine: false);
        return this;
    }

    public PdfReportEngine AddLineChart(string chartTitle, List<string> categories, List<PdfChartSeries> series,
        string valuePrefix = "", string valueSuffix = "")
    {
        DrawChart(chartTitle, categories, series, valuePrefix, valueSuffix, isLine: true);
        return this;
    }

    public PdfReportEngine AddTable(string? sectionTitle, string[] headers, double[] columnWidths, bool[] rightAlign,
        List<PdfTableRow> rows, string? emptyMessage = null)
    {
        if (sectionTitle != null) AddSectionTitle(sectionTitle);

        const double headerRowHeight = 20;
        const double normalRowHeight = 16;
        const double emphasisRowHeight = 18;

        void DrawHeaderRow()
        {
            EnsureSpace(headerRowHeight);
            var (cr, cg, cb) = HexToRgb(BrandCharcoalHex);
            var (gr, gg, gb) = HexToRgb(BrandGoldHex);
            // Charcoal header with gold underline for executive table look
            DrawRect(_current, Margin, _cursorY - headerRowHeight, ContentWidth, headerRowHeight, cr, cg, cb);
            DrawRect(_current, Margin, _cursorY - headerRowHeight, ContentWidth, 2, gr, gg, gb);
            double x = Margin;
            for (int c = 0; c < headers.Length; c++)
            {
                double w = columnWidths[c];
                string t = Truncate(headers[c], w - 8, 7.5, true);
                if (rightAlign.Length > c && rightAlign[c])
                    DrawTextRightAligned(_current, x + w - 5, _cursorY - 13, t, 7.5, bold: true, 1, 1, 1);
                else
                    DrawText(_current, x + 5, _cursorY - 13, t, 7.5, bold: true, 1, 1, 1);
                x += w;
            }
            _cursorY -= headerRowHeight;
        }

        DrawHeaderRow();

        if (rows.Count == 0)
        {
            EnsureSpace(normalRowHeight);
            DrawText(_current, Margin + 4, _cursorY - 11, emptyMessage ?? "No records match the current search or filter criteria.",
                8.5, bold: false, 0.4, 0.4, 0.4);
            _cursorY -= normalRowHeight;
        }

        bool zebra = false;
        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            double rowHeight = row.Style == PdfRowStyle.Normal ? normalRowHeight : emphasisRowHeight;

            if (_cursorY - rowHeight < ContentBottom)
            {
                EndPage();
                BeginPage();
                AddSectionTitle((sectionTitle ?? "Table") + " (continued)");
                DrawHeaderRow();
                zebra = false;
            }

            double x = Margin;
            switch (row.Style)
            {
                case PdfRowStyle.GroupHeader:
                {
                    var (sr, sg, sb) = HexToRgb(BrandSandHex);
                    var (agr, agg, agb) = HexToRgb(BrandGoldHex);
                    DrawRect(_current, Margin, _cursorY - rowHeight, ContentWidth, rowHeight, sr, sg, sb);
                    DrawRect(_current, Margin, _cursorY - rowHeight, 3.5, rowHeight, agr, agg, agb);
                    break;
                }
                case PdfRowStyle.Subtotal:
                {
                    var (sr, sg, sb) = HexToRgb(BrandSandHex);
                    var (gr, gg, gb) = HexToRgb(BrandGoldHex);
                    DrawRect(_current, Margin, _cursorY - rowHeight, ContentWidth, rowHeight, sr, sg, sb);
                    DrawLine(_current, Margin, _cursorY, PageWidth - Margin, _cursorY, gr, gg, gb, 0.7);
                    break;
                }
                case PdfRowStyle.GrandTotal:
                {
                    var (cr2, cg2, cb2) = HexToRgb(BrandCharcoalHex);
                    var (gr, gg, gb) = HexToRgb(BrandGoldHex);
                    DrawRect(_current, Margin, _cursorY - rowHeight, ContentWidth, rowHeight, cr2, cg2, cb2);
                    DrawRect(_current, Margin, _cursorY - 2, ContentWidth, 2, gr, gg, gb);
                    break;
                }
                default:
                    if (zebra) DrawRect(_current, Margin, _cursorY - rowHeight, ContentWidth, rowHeight, 0.975, 0.97, 0.96);
                    // subtle bottom hairline
                    DrawLine(_current, Margin, _cursorY - rowHeight, PageWidth - Margin, _cursorY - rowHeight, 0.90, 0.89, 0.87, 0.4);
                    zebra = !zebra;
                    break;
            }

            bool bold = row.Style != PdfRowStyle.Normal;
            bool white = row.Style == PdfRowStyle.GrandTotal;
            double tr = white ? 1 : 0.10, tg = white ? 1 : 0.10, tb = white ? 1 : 0.10;
            double baselineY = _cursorY - (rowHeight - 5);

            for (int c = 0; c < row.Cells.Length && c < columnWidths.Length; c++)
            {
                double w = columnWidths[c];
                string t = Truncate(row.Cells[c], w - 8, 8, bold);
                if (rightAlign.Length > c && rightAlign[c])
                    DrawTextRightAligned(_current, x + w - 4, baselineY, t, 8, bold, tr, tg, tb);
                else
                    DrawText(_current, x + 4, baselineY, t, 8, bold, tr, tg, tb);
                x += w;
            }

            _cursorY -= rowHeight;
        }

        _cursorY -= BlockGap - 4;
        return this;
    }

    public byte[] Build()
    {
        EndPage();

        int totalPages = _pages.Count;
        for (int i = 0; i < _pages.Count; i++)
            _pages[i] = _pages[i].Replace("@@TOTAL@@", totalPages.ToString(CultureInfo.InvariantCulture));

        var objects = new List<string> { "" };                                                  // 0 filler
        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");                                        // 1 catalog
        objects.Add("");                                                                          // 2 pages (filled below)
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");                   // 3 F1
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");               // 4 F1B
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Oblique >>");           // 5 F1I

        int logoId = 0;
        if (LogoBytes is { Length: > 0 })
        {
            logoId = objects.Count;
            var latin1Image = Encoding.Latin1.GetString(LogoBytes);
            objects.Add($"<< /Type /XObject /Subtype /Image /Width {LogoPxWidth} /Height {LogoPxHeight} " +
                        $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {LogoBytes.Length} >>\n" +
                        $"stream\n{latin1Image}\nendstream");
        }

        string resourceDict = logoId > 0
            ? $"<< /Font << /F1 3 0 R /F1B 4 0 R /F1I 5 0 R >> /XObject << /Logo {logoId} 0 R >> >>"
            : "<< /Font << /F1 3 0 R /F1B 4 0 R /F1I 5 0 R >> >>";

        var pageObjIds = new List<int>();
        foreach (var pageContent in _pages)
        {
            int contentId = objects.Count;
            objects.Add($"<< /Length {Encoding.Latin1.GetByteCount(pageContent)} >>\nstream\n{pageContent}\nendstream");
            int pageId = objects.Count;
            objects.Add($"<< /Type /Page /Parent 2 0 R /Resources {resourceDict} " +
                        $"/MediaBox [0 0 {PageWidth} {PageHeight}] /Contents {contentId} 0 R >>");
            pageObjIds.Add(pageId);
        }

        objects[2] = $"<< /Type /Pages /Kids [{string.Join(" ", pageObjIds.Select(id => $"{id} 0 R"))}] /Count {pageObjIds.Count} >>";

        return AssemblePdf(objects);
    }

    // =====================================================================
    // Page lifecycle
    // =====================================================================

    private void BeginPage()
    {
        _pageNum++;
        _current = new StringBuilder();
        DrawHeader();
        DrawFooter();
        _cursorY = ContentTop;
    }

    private void EndPage() => _pages.Add(_current.ToString());

    private void EnsureSpace(double neededHeight)
    {
        if (_cursorY - neededHeight < ContentBottom)
        {
            EndPage();
            BeginPage();
        }
    }

    private void DrawHeader()
    {
        var (gr, gg, gb) = HexToRgb(BrandGoldHex);
        var (cr, cg, cb) = HexToRgb(BrandCharcoalHex);
        var (dr, dg, db) = HexToRgb(BrandGoldDeepHex);
        var (sr, sg, sb) = HexToRgb(BrandSandHex);

        // Dual top accent: charcoal band + gold underline (executive letterhead)
        DrawRect(_current, 0, PageHeight - 10, PageWidth, 10, cr, cg, cb);
        DrawRect(_current, 0, PageHeight - 13, PageWidth, 3, gr, gg, gb);

        double logoSize = 36;
        double logoX = Margin, logoY = PageHeight - 28 - logoSize;
        if (LogoBytes is { Length: > 0 })
        {
            _current.Append($"q {Fmt(logoSize)} 0 0 {Fmt(logoSize)} {Fmt(logoX)} {Fmt(logoY)} cm /Logo Do Q\n");
        }

        double textX = logoX + logoSize + 11;
        double wordmarkY = logoY + logoSize - 12;
        DrawText(_current, textX, wordmarkY, "EQUAMERIDIAN", 13.5, bold: true, cr, cg, cb);
        double blackWidth = TextWidth("EQUAMERIDIAN", 13.5, true) + 5;
        DrawText(_current, textX + blackWidth, wordmarkY, "HOLDINGS", 13.5, bold: true, gr, gg, gb);
        DrawText(_current, textX, wordmarkY - 12, "Machinery Marketplace", 7, bold: false, 0.48, 0.46, 0.42);

        // Title block
        double y = logoY - 18;
        DrawText(_current, Margin, y, _title, 15, bold: true, cr, cg, cb);
        y -= 14;

        // Always reserve subtitle line height so ContentTop stays constant
        if (!string.IsNullOrWhiteSpace(_subtitle))
            DrawTextOblique(_current, Margin, y, _subtitle!, 8.5, 0.38, 0.36, 0.33);
        y -= 12;

        // Meta strip on sand
        double metaH = 16;
        DrawRect(_current, Margin, y - metaH + 4, ContentWidth, metaH, sr, sg, sb);
        string meta = $"Generated by {_generatedBy}   ·   {_generatedAt:dd MMM yyyy, HH:mm} SAST   ·   Page {_pageNum} of @@TOTAL@@";
        DrawText(_current, Margin + 6, y - 6, meta, 7, bold: false, 0.40, 0.38, 0.35);
        y -= metaH + 2;

        // Double rule: gold + charcoal hairline
        DrawLine(_current, Margin, y, PageWidth - Margin, y, gr, gg, gb, 1.4);
        DrawLine(_current, Margin, y - 2.5, PageWidth - Margin, y - 2.5, cr, cg, cb, 0.4);
    }

    private void DrawFooter()
    {
        var (gr, gg, gb) = HexToRgb(BrandGoldHex);
        var (cr, cg, cb) = HexToRgb(BrandCharcoalHex);

        // Gold rule above footer
        DrawLine(_current, Margin, ContentBottom - 14, PageWidth - Margin, ContentBottom - 14, gr, gg, gb, 0.8);
        // Charcoal footer bar
        DrawRect(_current, 0, 0, PageWidth, 22, cr, cg, cb);
        DrawRect(_current, 0, 22, PageWidth, 2, gr, gg, gb);

        DrawText(_current, Margin, 8, "CONFIDENTIAL  ·  EquaMeridian Holdings  ·  Machinery Marketplace",
            7, bold: false, 0.85, 0.82, 0.75);
        DrawTextRightAligned(_current, PageWidth - Margin, 8, $"Page {_pageNum} of @@TOTAL@@",
            7, bold: false, gr, gg, gb);
    }

    // =====================================================================
    // Charts
    // =====================================================================

    private void DrawChart(string chartTitle, List<string> categories, List<PdfChartSeries> series,
        string valuePrefix, string valueSuffix, bool isLine)
    {
        const double blockHeight = 262;
        EnsureSpace(blockHeight);

        var (cr, cg, cb) = HexToRgb(BrandCharcoalHex);
        var (gr, gg, gb) = HexToRgb(BrandGoldHex);
        var (sr, sg, sb) = HexToRgb(BrandSandHex);

        // Section tick + title
        DrawRect(_current, Margin, _cursorY - 14, 3.5, 12, gr, gg, gb);
        DrawText(_current, Margin + 10, _cursorY - 12, chartTitle, 11, bold: true, cr, cg, cb);

        // Legend with rounded-look squares
        double legendY = _cursorY - 30;
        double legendX = Margin + 10;
        for (int i = 0; i < series.Count; i++)
        {
            string hex = string.IsNullOrEmpty(series[i].ColorHex) ? ChartPalette[i % ChartPalette.Length] : series[i].ColorHex;
            var (r, g, b) = HexToRgb(hex);
            DrawRect(_current, legendX, legendY, 10, 10, r, g, b);
            DrawText(_current, legendX + 14, legendY + 1.5, series[i].Name, 7.5, bold: false, 0.22, 0.20, 0.18);
            legendX += 18 + TextWidth(series[i].Name, 7.5, false) + 16;
        }

        double chartTop = legendY - 12;
        double chartHeight = 168;
        double chartBottom = chartTop - chartHeight;
        double chartLeft = Margin + 48;
        double chartRight = PageWidth - Margin - 4;
        double chartWidth = chartRight - chartLeft;

        decimal maxValue = series.SelectMany(s => s.Values).DefaultIfEmpty(0).Max();
        if (maxValue <= 0) maxValue = 1;
        decimal axisMax = RoundUpToNiceNumber(maxValue);

        // Chart plot area wash
        DrawRect(_current, chartLeft, chartBottom, chartWidth, chartHeight, 0.995, 0.993, 0.988);

        DrawLine(_current, chartLeft, chartBottom, chartRight, chartBottom, 0.45, 0.43, 0.40, 0.8);
        DrawLine(_current, chartLeft, chartBottom, chartLeft, chartTop, 0.45, 0.43, 0.40, 0.8);

        for (int g = 0; g <= 4; g++)
        {
            double gy = chartBottom + (chartHeight * g / 4.0);
            if (g > 0)
                DrawLine(_current, chartLeft, gy, chartRight, gy, 0.90, 0.88, 0.85, 0.45);
            decimal value = axisMax * g / 4;
            string label = valuePrefix == "R"
                ? CompactMoney(value) + valueSuffix
                : valuePrefix + value.ToString("N0", CultureInfo.InvariantCulture) + valueSuffix;
            DrawTextRightAligned(_current, chartLeft - 5, gy - 2.5, label, 6.5, false, 0.40, 0.38, 0.35);
        }

        int n = categories.Count;
        if (n > 0)
        {
            double slotWidth = chartWidth / n;

            if (!isLine)
            {
                int sc = Math.Max(1, series.Count);
                double groupWidth = Math.Min(slotWidth * 0.7, sc * 14);
                double barWidth = groupWidth / sc;

                for (int i = 0; i < n; i++)
                {
                    double slotCenter = chartLeft + (slotWidth * i) + (slotWidth / 2);
                    double groupLeft = slotCenter - (groupWidth / 2);
                    for (int s = 0; s < series.Count; s++)
                    {
                        decimal val = i < series[s].Values.Count ? series[s].Values[i] : 0;
                        double h = (double)(val / axisMax) * chartHeight;
                        string hex = string.IsNullOrEmpty(series[s].ColorHex) ? ChartPalette[s % ChartPalette.Length] : series[s].ColorHex;
                        var (r, g2, b2) = HexToRgb(hex);
                        DrawRect(_current, groupLeft + (s * barWidth) + 1, chartBottom, barWidth - 2, Math.Max(h, 0.5), r, g2, b2);
                    }
                    DrawTextCentered(_current, slotCenter, chartBottom - 11, categories[i], 6.5, 0.3, 0.3, 0.3);
                }
            }
            else
            {
                for (int s = 0; s < series.Count; s++)
                {
                    string hex = string.IsNullOrEmpty(series[s].ColorHex) ? ChartPalette[s % ChartPalette.Length] : series[s].ColorHex;
                    var (r, g2, b2) = HexToRgb(hex);
                    var points = new List<(double x, double y)>();
                    for (int i = 0; i < n; i++)
                    {
                        decimal val = i < series[s].Values.Count ? series[s].Values[i] : 0;
                        double px = chartLeft + (slotWidth * i) + (slotWidth / 2);
                        double py = chartBottom + (double)(val / axisMax) * chartHeight;
                        points.Add((px, py));
                    }
                    for (int i = 0; i < points.Count - 1; i++)
                        DrawLine(_current, points[i].x, points[i].y, points[i + 1].x, points[i + 1].y, r, g2, b2, 1.6);
                    foreach (var p in points)
                        DrawRect(_current, p.x - 2, p.y - 2, 4, 4, r, g2, b2);
                }
                for (int i = 0; i < n; i++)
                {
                    double slotCenter = chartLeft + (slotWidth * i) + (slotWidth / 2);
                    DrawTextCentered(_current, slotCenter, chartBottom - 11, categories[i], 6.5, 0.3, 0.3, 0.3);
                }
            }
        }

        _cursorY -= blockHeight;
    }

    // =====================================================================
    // Low-level drawing primitives
    // =====================================================================

    private static void DrawRect(StringBuilder sb, double x, double y, double w, double h, double r, double g, double b)
    {
        if (h < 0) h = 0;
        sb.Append($"{Fmt(r)} {Fmt(g)} {Fmt(b)} rg\n{Fmt(x)} {Fmt(y)} {Fmt(w)} {Fmt(h)} re\nf\n");
    }

    private static void DrawLine(StringBuilder sb, double x1, double y1, double x2, double y2, double r, double g, double b, double width)
    {
        sb.Append($"{Fmt(r)} {Fmt(g)} {Fmt(b)} RG\n{Fmt(width)} w\n{Fmt(x1)} {Fmt(y1)} m\n{Fmt(x2)} {Fmt(y2)} l\nS\n");
    }

    /// <summary>Fills one annulus sector (a "donut slice") as a polygon approximation of the arc —
    /// outer arc forward then inner arc backward, closed and filled. startAngleDeg/sweepDeg use
    /// standard math convention (0=east, 90=north); sweep is consumed clockwise (angle decreases).</summary>
    private static void DrawDonutSlice(StringBuilder sb, double cx, double cy, double outerR, double innerR,
        double startAngleDeg, double sweepDeg, double r, double g, double b)
    {
        if (sweepDeg <= 0) return;
        int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweepDeg) / 3.0));

        sb.Append($"{Fmt(r)} {Fmt(g)} {Fmt(b)} rg\n");

        double a0 = startAngleDeg * Math.PI / 180.0;
        sb.Append($"{Fmt(cx + outerR * Math.Cos(a0))} {Fmt(cy + outerR * Math.Sin(a0))} m\n");
        for (int i = 1; i <= steps; i++)
        {
            double a = (startAngleDeg - sweepDeg * i / steps) * Math.PI / 180.0;
            sb.Append($"{Fmt(cx + outerR * Math.Cos(a))} {Fmt(cy + outerR * Math.Sin(a))} l\n");
        }
        for (int i = steps; i >= 0; i--)
        {
            double a = (startAngleDeg - sweepDeg * i / steps) * Math.PI / 180.0;
            sb.Append($"{Fmt(cx + innerR * Math.Cos(a))} {Fmt(cy + innerR * Math.Sin(a))} l\n");
        }
        sb.Append("h\nf\n");
    }

    private static void DrawText(StringBuilder sb, double x, double y, string text, double fontSize, bool bold, double r, double g, double b)
    {
        string font = bold ? "F1B" : "F1";
        sb.Append("BT\n").Append($"/{font} {Fmt(fontSize)} Tf\n{Fmt(r)} {Fmt(g)} {Fmt(b)} rg\n")
          .Append($"{Fmt(x)} {Fmt(y)} Td\n({Escape(text)}) Tj\nET\n");
    }

    private static void DrawTextOblique(StringBuilder sb, double x, double y, string text, double fontSize, double r, double g, double b)
    {
        sb.Append("BT\n").Append($"/F1I {Fmt(fontSize)} Tf\n{Fmt(r)} {Fmt(g)} {Fmt(b)} rg\n")
          .Append($"{Fmt(x)} {Fmt(y)} Td\n({Escape(text)}) Tj\nET\n");
    }

    private static void DrawTextRightAligned(StringBuilder sb, double rightX, double y, string text, double fontSize, bool bold, double r, double g, double b)
    {
        double w = TextWidth(text, fontSize, bold);
        DrawText(sb, rightX - w, y, text, fontSize, bold, r, g, b);
    }

    private static void DrawTextCentered(StringBuilder sb, double centerX, double y, string text, double fontSize, double r, double g, double b)
    {
        double w = TextWidth(text, fontSize, false);
        DrawText(sb, centerX - (w / 2), y, text, fontSize, false, r, g, b);
    }

    // Helvetica metrics approximation: bold caps are ~0.72em wide (was 0.56 — caused HOLDINGS overlap).
    private static double TextWidth(string text, double fontSize, bool bold) => text.Length * fontSize * (bold ? 0.72 : 0.55);

    private static string Truncate(string text, double maxWidth, double fontSize, bool bold)
    {
        if (maxWidth <= 0 || TextWidth(text, fontSize, bold) <= maxWidth) return text;
        while (text.Length > 1 && TextWidth(text + "...", fontSize, bold) > maxWidth)
            text = text[..^1];
        return text.Length <= 1 ? text : text + "...";
    }

    private static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Escape(string text) =>
        (text ?? string.Empty).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static List<string> WrapText(string text, double maxWidth, double fontSize, bool bold)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) { result.Add(""); return result; }
        var words = text.Split(' ');
        var line = new StringBuilder();
        foreach (var word in words)
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (TextWidth(candidate, fontSize, bold) <= maxWidth)
            {
                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }
            else
            {
                if (line.Length > 0) result.Add(line.ToString());
                line.Clear();
                line.Append(word);
            }
        }
        if (line.Length > 0) result.Add(line.ToString());
        return result;
    }

    private static string CompactMoney(decimal value)
    {
        if (Math.Abs(value) >= 1_000_000m)
            return "R" + (value / 1_000_000m).ToString("0.##", CultureInfo.InvariantCulture) + "M";
        if (Math.Abs(value) >= 1_000m)
            return "R" + (value / 1_000m).ToString("0.#", CultureInfo.InvariantCulture) + "k";
        return "R" + value.ToString("N0", CultureInfo.InvariantCulture);
    }

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

    private static byte[]? LoadLogoBytes()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("equameridian-logo.jpg", StringComparison.OrdinalIgnoreCase));
            if (resourceName == null) return null;
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

   
    private static byte[] AssemblePdf(List<string> objects)
    {
        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n");

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