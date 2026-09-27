using System.Globalization;
using EquaMeridian.DTOs.Invoices;

public static class InvoicePdfService
{
    public static byte[] Build(InvoiceDto inv)
    {
        var invC = CultureInfo.InvariantCulture;
        string Money(decimal n) => "R " + n.ToString("#,##0.00", invC);
        string Day(DateTime d) => d.ToString("dd MMMM yyyy", invC);

        string Row(string label, string value) => label.PadRight(34) + value.PadLeft(16);
        string MoneyRow(string label, decimal amount) => Row(label, Money(amount));

        var lines = new List<string>
        {
            "TAX INVOICE  |  VAT Act 89 of 1991",
            "",
            Row("Invoice number", inv.InvoiceNumber ?? ""),
            Row("Invoice date", Day(inv.InvoiceDate)),
            Row("Due date", Day(inv.DueDate)),
            Row("Payment status", inv.PaymentStatus ?? ""),
            Row("Payment method", string.IsNullOrWhiteSpace(inv.PaymentMethod) ? (inv.TotalAmount > 50000m ? "EFT" : "PayFast") : inv.PaymentMethod),
            Row("Currency", inv.Currency ?? "ZAR"),
            "",
            "FROM (SUPPLIER)",
            "  " + (inv.SupplierName ?? "-"),
            "",
            "BILL TO (CONTRACTOR)",
            "  " + (inv.ContractorName ?? "-"),
            "",
            "--------------------------------------------------------------------------------",
            Row("Description", "Amount (ZAR)"),
            "--------------------------------------------------------------------------------",
            Row("Plant hire - " + (inv.ListingTitle ?? "Equipment"), Money(inv.Subtotal)),
            MoneyRow("Discount", inv.DiscountAmount),
            MoneyRow("Delivery", inv.DeliveryFee),
            MoneyRow($"VAT @ {inv.VATRate.ToString("0.##", invC)}%", inv.VATAmount),
            "--------------------------------------------------------------------------------",
            Row("TOTAL INCLUSIVE OF VAT", Money(inv.TotalAmount)),
            "--------------------------------------------------------------------------------",
            "",
            "This tax invoice is generated from EquaMeridian transactional records.",
            "All amounts are South African Rand (ZAR). Prices include VAT where indicated.",
        };

        return SimplePdfWriter.Generate(
            $"TAX INVOICE  {inv.InvoiceNumber}",
            lines,
            "EquaMeridian Accounts");
    }
}
