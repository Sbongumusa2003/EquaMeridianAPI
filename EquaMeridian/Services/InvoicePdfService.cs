using System.Globalization;
using EquaMeridian.DTOs.Invoices;

public static class InvoicePdfService
{
    public static byte[] Build(InvoiceDto inv)
    {
        var invC = CultureInfo.InvariantCulture;
        string Money(decimal n) => "R " + n.ToString("#,##0.00", invC);
        string Day(DateTime d) => d.ToString("dd MMMM yyyy", invC);
        string ShortDay(DateTime d) => d.ToString("dd MMM yyyy", invC);

        const int LabelW = 42;
        const int ValueW = 18;
        string Row(string label, string value)
        {
            var l = (label ?? "").Length > LabelW ? (label ?? "")[..LabelW] : (label ?? "");
            var v = (value ?? "").Length > ValueW ? (value ?? "")[..ValueW] : (value ?? "");
            return l.PadRight(LabelW) + v.PadLeft(ValueW);
        }
        string MoneyRow(string label, decimal amount) => Row(label, Money(amount));
        string Rule() => new string('-', LabelW + ValueW);

        var qty = Math.Max(1, inv.Quantity);
        var period = inv.RentalStartDate.HasValue && inv.RentalEndDate.HasValue
            ? $"{ShortDay(inv.RentalStartDate.Value)} – {ShortDay(inv.RentalEndDate.Value)}"
            : "—";
        var days = inv.RentalDays is > 0
            ? inv.RentalDays.Value
            : (inv.RentalStartDate.HasValue && inv.RentalEndDate.HasValue
                ? Math.Max(1, (inv.RentalEndDate.Value.Date - inv.RentalStartDate.Value.Date).Days)
                : (int?)null);
        if (days == 0) days = 1;

        var desc = "Plant hire – " + (string.IsNullOrWhiteSpace(inv.ListingTitle) ? "Equipment" : inv.ListingTitle.Trim());
        var rateLine = inv.DailyRateZAR is > 0 && days is > 0
            ? $"{Money(inv.DailyRateZAR.Value)}/day × {days} day(s) × {qty} unit(s)"
            : (days is > 0 ? $"{days} day(s) × {qty} unit(s)" : $"{qty} unit(s)");

        var lines = new List<string>
        {
            "TAX INVOICE  |  VAT Act 89 of 1991",
            "",
            Row("Invoice number", inv.InvoiceNumber ?? ""),
            Row("Invoice date", Day(inv.InvoiceDate)),
            Row("Due date", Day(inv.DueDate)),
            Row("Payment status", inv.PaymentStatus ?? ""),
            Row("Payment method",
                string.IsNullOrWhiteSpace(inv.PaymentMethod)
                    ? (inv.TotalAmount > 50000m ? "EFT" : "PayFast")
                    : inv.PaymentMethod),
            Row("Currency", inv.Currency ?? "ZAR"),
            "",
            "FROM (SUPPLIER)",
            "  " + (inv.SupplierName ?? "—"),
            "",
            "BILL TO (CONTRACTOR)",
            "  " + (inv.ContractorName ?? "—"),
            "",
            Rule(),
            Row("Description", "Amount (ZAR)"),
            Rule(),
            desc,
            "  Qty: " + qty,
            "  Rental period: " + period,
            "  " + rateLine,
            Row("  Hire subtotal", Money(inv.Subtotal)),
            "",
        };

        if (inv.DiscountAmount > 0)
            lines.Add(MoneyRow("Discount", -inv.DiscountAmount));

        if (inv.DeliveryFee > 0)
            lines.Add(MoneyRow("Delivery / transport", inv.DeliveryFee));

        lines.Add(MoneyRow($"VAT @ {inv.VATRate.ToString("0.##", invC)}%", inv.VATAmount));
        lines.Add(Rule());
        lines.Add(Row("TOTAL INCLUSIVE OF VAT", Money(inv.TotalAmount)));
        lines.Add(Rule());
        lines.Add("");
        lines.Add("This tax invoice is generated from EquaMeridian transactional records.");
        lines.Add("All amounts are South African Rand (ZAR). VAT is shown where applicable.");

        return SimplePdfWriter.Generate(
            $"TAX INVOICE  {inv.InvoiceNumber}",
            lines,
            "EquaMeridian Accounts");
    }
}
