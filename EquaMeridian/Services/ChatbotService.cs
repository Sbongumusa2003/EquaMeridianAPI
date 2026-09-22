using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using EquaMeridian.DTOs.Chatbot;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using static ChatbotIntentModel.Intents;

/// <summary>
/// Domain chatbot: ML.NET intent classification + multi-turn session memory +
/// entity extraction + live DB lookups. Training data is self-authored for EquaMeridian.
/// </summary>
public class ChatbotService : IChatbotService
{
    private const float ConfidenceThreshold = 0.28f;

    private readonly ChatbotIntentModel _model;
    private readonly AppDbContext _db;

    /// <summary>In-memory multi-turn context keyed by user or anonymous session.</summary>
    private static readonly ConcurrentDictionary<string, ChatSession> Sessions = new();

    public ChatbotService(ChatbotIntentModel model, AppDbContext db)
    {
        _model = model;
        _db = db;
    }

    public async Task<ChatbotReplyDto> AskAsync(string message, int? userId, string? role, string? sessionId = null)
    {
        var sid = string.IsNullOrWhiteSpace(sessionId)
            ? (userId?.ToString() ?? "anon")
            : sessionId.Trim();

        var session = Sessions.GetOrAdd(sid, _ => new ChatSession());
        var normalized = (message ?? string.Empty).Trim();
        var entities = ExtractEntities(normalized);

        // Carry forward booking/invoice ids from prior turns when user says "that booking"
        if (entities.BookingId is null && session.LastBookingId is not null
            && Regex.IsMatch(normalized, @"\b(that|this|my|the)\s+(booking|hire|rental)\b", RegexOptions.IgnoreCase))
            entities.BookingId = session.LastBookingId;

        if (entities.InvoiceNumber is null && session.LastInvoiceNumber is not null
            && Regex.IsMatch(normalized, @"\b(that|this|my|the)\s+invoice\b", RegexOptions.IgnoreCase))
            entities.InvoiceNumber = session.LastInvoiceNumber;

        var (intent, confidence, topIntents) = _model.Predict(normalized);

        // Follow-up: if user asks a short clarification after a status intent, reuse last intent
        if (confidence < ConfidenceThreshold && session.LastIntent is not null
            && normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4
            && Regex.IsMatch(normalized, @"^(and|what about|how about|also|status|update|more)\b", RegexOptions.IgnoreCase))
        {
            intent = session.LastIntent;
            confidence = Math.Max(confidence, 0.45f);
        }

        ChatbotReplyDto reply;
        if (confidence < ConfidenceThreshold)
        {
            reply = new ChatbotReplyDto
            {
                Intent = "LowConfidence",
                Confidence = confidence,
                Reply = "I'm not completely sure what you're asking. I use a self-trained intent model for EquaMeridian — try one of these:",
                QuickReplies = new()
                {
                    "How do bookings work?",
                    "What is the status of my booking?",
                    "Where is my invoice?",
                    "How do I raise a dispute?",
                    "What is EquaMeridian?"
                },
                TopIntents = topIntents.Select(t => new ChatbotIntentScoreDto { Intent = t.Intent, Score = t.Score }).ToList(),
                SessionId = sid
            };
        }
        else
        {
            reply = await RouteAsync(intent, userId, role, entities);
            reply.Intent = intent;
            reply.Confidence = confidence;
            reply.TopIntents = topIntents.Select(t => new ChatbotIntentScoreDto { Intent = t.Intent, Score = t.Score }).ToList();
            reply.SessionId = sid;
            reply.Entities = new ChatbotEntitiesDto
            {
                BookingId = entities.BookingId,
                InvoiceNumber = entities.InvoiceNumber
            };
        }

        session.LastIntent = reply.Intent is "LowConfidence" ? session.LastIntent : reply.Intent;
        if (entities.BookingId is not null) session.LastBookingId = entities.BookingId;
        if (entities.InvoiceNumber is not null) session.LastInvoiceNumber = entities.InvoiceNumber;
        session.TurnCount++;
        session.LastMessageUtc = DateTime.UtcNow;

        return reply;
    }

    public ChatbotModelInfo GetModelInfo() => _model.GetModelInfo();

    public void RecordFeedback(ChatbotFeedbackDto dto)
    {
        // Lightweight in-process feedback log for exam demos / future retraining
        FeedbackLog.Add(new FeedbackEntry
        {
            SessionId = dto.SessionId,
            Intent = dto.Intent,
            Helpful = dto.Helpful,
            Comment = dto.Comment,
            AtUtc = DateTime.UtcNow
        });
    }

    public IReadOnlyList<object> GetFeedbackSummary()
    {
        var list = FeedbackLog.ToList();
        return new object[]
        {
            new
            {
                total = list.Count,
                helpful = list.Count(f => f.Helpful),
                notHelpful = list.Count(f => !f.Helpful),
                recent = list.OrderByDescending(f => f.AtUtc).Take(20)
                    .Select(f => new { f.Intent, f.Helpful, f.Comment, f.AtUtc })
            }
        };
    }

    private async Task<ChatbotReplyDto> RouteAsync(string intent, int? userId, string? role, ExtractedEntities entities)
    {
        var isSupplier = string.Equals(role, "Supplier", StringComparison.OrdinalIgnoreCase);
        var isContractor = string.Equals(role, "Contractor", StringComparison.OrdinalIgnoreCase);
        var isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(role, "Internal", StringComparison.OrdinalIgnoreCase);

        return intent switch
        {
            Greeting => Static(
                "Hi! I'm the EquaMeridian assistant — trained on our hire marketplace (not a generic chatbot). " +
                "Ask about bookings, quotes, invoices, VAT, delivery, disputes or your account.",
                "How do bookings work?", "What is the status of my booking?", "What is EquaMeridian?"),

            Goodbye or SmallTalkThanks => Static("You're welcome — glad I could help. Come back any time."),

            PlatformOverview => Static(
                "EquaMeridian is a machinery hire marketplace: suppliers list equipment, contractors request quotes or book, " +
                "payments run via invoice/PayFast, and handovers are tracked (pickup or delivery) through to return.",
                "How do bookings work?", "How do quotations work?"),

            BookingHelp => isSupplier
                ? Static(
                    "As a supplier: when a contractor books or accepts a quote, confirm the booking, mark the machine Ready for Pickup/Delivery, " +
                    "then confirm return when it comes back. Track everything under Bookings & Deliveries.",
                    "What is the status of my booking?", "How does delivery work?")
                : Static(
                    "Browse listings → request a quote or book with dates and fulfilment (pickup or delivery). " +
                    "After payment/lease, track Confirmed → Ready → Delivered/Collected → return under Bookings & Deliveries.",
                    "What is the status of my booking?", "How does delivery work?"),

            BookingStatus => await BookingStatusReply(userId, entities.BookingId),

            InvoiceHelp => Static(
                "An invoice is generated when a quotation is accepted (or at checkout). It shows rental subtotal, VAT, platform fee and totals. " +
                "Find it under Transactions → Invoices.",
                "Where is my invoice?", "How is VAT calculated?"),

            InvoiceStatus => await InvoiceStatusReply(userId, entities.InvoiceNumber),

            VatQuestion => await VatReply(),

            DisputeHelp => Static(
                "Open the booking under Bookings & Deliveries and choose Raise dispute. Describe the issue and attach evidence. " +
                "Admin reviews and may process a refund or deposit deduction.",
                "What is the status of my booking?"),

            QuotationHelp => isSupplier
                ? Static(
                    "Open Quotation Requests, review dates/quantity, enter daily rate (and optional weekly rate, delivery fee), set validity, then submit. " +
                    "The contractor can accept or reject.",
                    "Status of my quotation")
                : Static(
                    "On a listing, request a quotation with dates and fulfilment. The supplier replies with rates; you can accept (creates booking + invoice + lease) or reject.",
                    "Status of my quotation", "Compare quotations"),

            QuotationStatus => await QuotationStatusReply(userId),

            InspectionStatus => await InspectionStatusReply(userId),

            ReviewHelp => Static(
                "After a completed hire, open the booking and leave a star rating and comment for the other party. Reviews help the marketplace stay trustworthy.",
                "What is the status of my booking?"),

            PaymentHelp => Static(
                "Pay outstanding invoices via PayFast from Transactions → Invoices (or the payment link). " +
                "Bookings stay blocked until payment succeeds where required.",
                "Where is my invoice?"),

            RefundPolicy => Static(
                "Refunds are handled through disputes or admin-approved refunds linked to invoices. " +
                "Deposits may be returned after return inspection if no deductions apply.",
                "How do I raise a dispute?"),

            PasswordHelp => Static(
                "Use Forgot password on the login screen. You'll get an OTP by email/SMS, then set a new password.",
                "Contact support"),

            ContactSupport => Static(
                "Use in-app Messages to contact the other party on a booking, or reach platform support via your registered email. " +
                "For urgent payment issues, include your invoice number.",
                "Where is my invoice?"),

            ListingHelp => isSupplier
                ? Static(
                    "My Listings → Create Listing: title, category, rates, location, 1–5 photos, accept the Master Lease Agreement, then submit for admin review.",
                    "How does delivery work?")
                : Static(
                    "Contractors browse approved listings, filter by category/location, and request quotes or book. You cannot list equipment on a contractor account.",
                    "How do bookings work?"),

            DeliveryHelp => Static(
                "Pickup: supplier marks Ready for Collection, contractor confirms Collected. " +
                "Supplier delivery: optional Ready for Delivery, then contractor confirms Delivered to You. " +
                "Return flow mirrors this until Returned to Supplier.",
                "What is the status of my booking?"),

            LeaseHelp => Static(
                "Each accepted hire generates a lease agreement from the Master Lease Agreement terms. Sign it under Lease Agreements before equipment is released where required.",
                "How do bookings work?"),

            PromoHelp => Static(
                "Active campaigns may offer promo codes or automatic discounts at checkout. Enter a code in cart/checkout if you have one; admin configures campaigns under Campaigns.",
                "How do I pay?"),

            AccountHelp => Static(
                "Keep profile and verification documents up to date. Contractors need approved documents before purchasing. " +
                "Accounts in an active booking process cannot be deactivated.",
                "I forgot my password"),

            PayoutHelp => isSupplier
                ? await PayoutReply(userId)
                : Static("Payouts apply to suppliers after completed paid hires. Contractors pay invoices; they do not receive supplier payouts."),

            CompareQuotes => Static(
                "Open Quotations and use Compare (select 2–3 quotes for the same need) to see rates, totals and supplier side by side before accepting.",
                "How do quotations work?"),

            SafetyInsurance => Static(
                "Liability, damage waiver and insurance expectations are set out in the Master Lease Agreement and each booking lease. " +
                "Report damage via disputes and condition inspections.",
                "Master lease agreement"),

            _ => Static(
                "I can help with bookings, quotations, invoices, VAT, delivery, disputes, listings, payouts and account questions.",
                "How do bookings work?", "Where is my invoice?", "What is EquaMeridian?")
        };
    }

    private static ChatbotReplyDto Static(string text, params string[] quickReplies) => new()
    {
        Reply = text,
        QuickReplies = quickReplies.ToList()
    };

    private static ExtractedEntities ExtractEntities(string message)
    {
        var e = new ExtractedEntities();
        var booking = Regex.Match(message, @"\bbooking\s*#?\s*(\d+)\b", RegexOptions.IgnoreCase);
        if (booking.Success && int.TryParse(booking.Groups[1].Value, out var bid))
            e.BookingId = bid;

        var inv = Regex.Match(message, @"\b(INV[-\s]?\d+|\binvoice\s*#?\s*([A-Za-z0-9-]+))\b", RegexOptions.IgnoreCase);
        if (inv.Success)
            e.InvoiceNumber = inv.Groups[2].Success ? inv.Groups[2].Value : inv.Groups[1].Value.Replace(" ", "");

        return e;
    }

    private async Task<ChatbotReplyDto> BookingStatusReply(int? userId, int? bookingId)
    {
        if (userId is null)
            return Static("Log in and I can look up your live booking status from the database.");

        var query = _db.Bookings.AsNoTracking()
            .Where(b => b.ContractorID == userId || b.SupplierID == userId);
        if (bookingId is not null)
            query = query.Where(b => b.BookingID == bookingId.Value);

        var booking = await query
            .OrderByDescending(b => b.CreatedDate)
            .Select(b => new { b.BookingID, b.Status, b.RentalStartDate, b.RentalEndDate })
            .FirstOrDefaultAsync();

        if (booking is null)
            return Static(bookingId is null
                ? "You don't have any bookings yet. Browse listings to get started."
                : $"I couldn't find booking #{bookingId} on your account.");

        return Static(
            $"Booking #{booking.BookingID} is '{booking.Status}' " +
            $"({booking.RentalStartDate:d MMM yyyy} – {booking.RentalEndDate:d MMM yyyy}). " +
            "See Bookings & Deliveries for the full tracker.",
            "How does delivery work?", "Where is my invoice?");
    }

    private async Task<ChatbotReplyDto> InvoiceStatusReply(int? userId, string? invoiceNumber)
    {
        if (userId is null)
            return Static("Log in and I can look up your invoices from the database.");

        var query = _db.Invoices.AsNoTracking()
            .Where(i => i.ContractorID == userId || i.SupplierID == userId);
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
            query = query.Where(i => i.InvoiceNumber == invoiceNumber);

        var invoice = await query
            .OrderByDescending(i => i.CreatedDate)
            .Select(i => new { i.InvoiceNumber, i.PaymentStatus, i.TotalAmount, i.Currency, i.DueDate })
            .FirstOrDefaultAsync();

        if (invoice is null)
            return Static(string.IsNullOrWhiteSpace(invoiceNumber)
                ? "You don't have any invoices yet — one is created when a quotation is accepted or checkout completes."
                : $"I couldn't find invoice {invoiceNumber} on your account.");

        return Static(
            $"Invoice {invoice.InvoiceNumber} is '{invoice.PaymentStatus}' for {invoice.Currency} {invoice.TotalAmount:N2}, " +
            $"due {invoice.DueDate:d MMM yyyy}.",
            "How is VAT calculated?", "How do I pay?");
    }

    private async Task<ChatbotReplyDto> QuotationStatusReply(int? userId)
    {
        if (userId is null)
            return Static("Log in to check live quotation status.");

        var q = await _db.Quotations.AsNoTracking()
            .Where(x => x.ContractorID == userId || x.SupplierID == userId)
            .OrderByDescending(x => x.RequestedDate)
            .Select(x => new { x.QuotationID, x.Status, x.Listing.ListingTitle, x.RequestedDate })
            .FirstOrDefaultAsync();

        if (q is null)
            return Static("No quotations found on your account yet.");

        return Static(
            $"Latest quotation #{q.QuotationID} on '{q.ListingTitle}' is '{q.Status}' (requested {q.RequestedDate:d MMM yyyy}).",
            "How do quotations work?");
    }

    private async Task<ChatbotReplyDto> InspectionStatusReply(int? userId)
    {
        if (userId is null)
            return Static("Log in and I can look up inspection status.");

        var inspection = await _db.Inspections.AsNoTracking()
            .Where(i => i.RequestedByAdminID == userId || i.Listing.SupplierID == userId)
            .OrderByDescending(i => i.InspectionID)
            .Select(i => new { i.InspectionID, i.Status, i.Outcome, i.ListingID })
            .FirstOrDefaultAsync();

        if (inspection is null)
            return Static("No inspections found for your account yet.");

        var outcomePart = inspection.Outcome is null ? "" : $" (outcome: {inspection.Outcome})";
        return Static($"Inspection #{inspection.InspectionID} for listing #{inspection.ListingID} is '{inspection.Status}'{outcomePart}.");
    }

    private async Task<ChatbotReplyDto> VatReply()
    {
        var vat = await _db.FeeConfigurations.AsNoTracking()
            .OrderByDescending(f => f.FeeConfigurationID)
            .Select(f => (decimal?)f.VATRate)
            .FirstOrDefaultAsync();

        var rate = vat ?? 15m;
        return Static(
            $"VAT is applied to the rental subtotal at the platform rate (currently {rate:0.##}%, configurable by admin under Platform Fees). " +
            "It appears as a separate line on invoices.",
            "Where is my invoice?");
    }

    private async Task<ChatbotReplyDto> PayoutReply(int? userId)
    {
        if (userId is null)
            return Static("Log in as a supplier to check payouts.");

        var payout = await _db.Payouts.AsNoTracking()
            .Where(p => p.SupplierID == userId)
            .OrderByDescending(p => p.PayoutID)
            .Select(p => new { p.PayoutID, p.Status, p.PayoutAmount })
            .FirstOrDefaultAsync();

        if (payout is null)
            return Static("No payouts recorded yet. Payouts appear after completed paid hires under Payouts.");

        return Static($"Your latest payout #{payout.PayoutID} is '{payout.Status}' for R {payout.PayoutAmount:N2}.");
    }

    private sealed class ChatSession
    {
        public string? LastIntent { get; set; }
        public int? LastBookingId { get; set; }
        public string? LastInvoiceNumber { get; set; }
        public int TurnCount { get; set; }
        public DateTime LastMessageUtc { get; set; }
    }

    private sealed class ExtractedEntities
    {
        public int? BookingId { get; set; }
        public string? InvoiceNumber { get; set; }
    }

    private sealed class FeedbackEntry
    {
        public string? SessionId { get; set; }
        public string? Intent { get; set; }
        public bool Helpful { get; set; }
        public string? Comment { get; set; }
        public DateTime AtUtc { get; set; }
    }

    private static readonly ConcurrentBag<FeedbackEntry> FeedbackLog = new();
}
