using EquaMeridian.DTOs.Chatbot;
using Microsoft.ML;
using Microsoft.ML.Data;

/// <summary>
/// Self-trained multiclass intent classifier for the EquaMeridian assistant.
/// Training examples are authored in-house (no external LLM). The model is fit at
/// startup with ML.NET SDCA Maximum Entropy over bag-of-words text features.
/// </summary>
public class ChatbotTrainingExample
{
    public string Text { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class ChatbotPrediction
{
    public string Intent { get; set; } = string.Empty;
    public float[] Score { get; set; } = Array.Empty<float>();
}

public class ChatbotIntentModel
{
    private readonly MLContext _ml = new(seed: 42);
    private readonly PredictionEngine<ChatbotTrainingExample, ChatbotPrediction> _engine;
    private readonly DataViewSchema _schema;
    private readonly string[] _labelOrder;
    private readonly object _lock = new();
    private readonly int _trainingExampleCount;
    private readonly double _holdoutAccuracy;

    public ChatbotIntentModel()
    {
        var all = BuildTrainingSet();
        _trainingExampleCount = all.Count;

        // 85/15 holdout so we can report real accuracy for exam demos
        var shuffled = all.OrderBy(_ => Guid.NewGuid()).ToList();
        var splitAt = Math.Max(1, (int)(shuffled.Count * 0.85));
        var train = shuffled.Take(splitAt).ToList();
        var test = shuffled.Skip(splitAt).ToList();
        if (test.Count == 0) test = train.Take(Math.Min(10, train.Count)).ToList();

        var trainingData = _ml.Data.LoadFromEnumerable(train);

        var pipeline = _ml.Transforms.Conversion.MapValueToKey(nameof(ChatbotTrainingExample.Label))
            .Append(_ml.Transforms.Text.FeaturizeText("Features", nameof(ChatbotTrainingExample.Text)))
            .Append(_ml.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                labelColumnName: "Label",
                featureColumnName: "Features"))
            .Append(_ml.Transforms.Conversion.MapKeyToValue(nameof(ChatbotPrediction.Intent), "PredictedLabel"));

        var model = pipeline.Fit(trainingData);
        _schema = trainingData.Schema;
        _engine = _ml.Model.CreatePredictionEngine<ChatbotTrainingExample, ChatbotPrediction>(model);

        // Evaluate holdout
        var correct = 0;
        foreach (var t in test)
        {
            var pred = _engine.Predict(t);
            if (string.Equals(pred.Intent, t.Label, StringComparison.OrdinalIgnoreCase))
                correct++;
        }
        _holdoutAccuracy = test.Count == 0 ? 0 : (double)correct / test.Count;

        // Label order from score vector is not stable across ML.NET versions; we expose intent names separately
        _labelOrder = all.Select(x => x.Label).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
    }

    public (string Intent, float Confidence, IReadOnlyList<(string Intent, float Score)> TopIntents) Predict(string text)
    {
        lock (_lock)
        {
            var prediction = _engine.Predict(new ChatbotTrainingExample { Text = text ?? string.Empty });
            var confidence = prediction.Score is { Length: > 0 } ? prediction.Score.Max() : 0f;

            // Pair scores with known labels when lengths match; otherwise just report predicted intent
            var top = new List<(string, float)>();
            if (prediction.Score is { Length: > 0 })
            {
                // When score length matches label count, rank; else surface predicted only
                if (prediction.Score.Length == _labelOrder.Length)
                {
                    top = _labelOrder
                        .Select((label, i) => (label, prediction.Score[i]))
                        .OrderByDescending(x => x.Item2)
                        .Take(3)
                        .ToList();
                }
                else
                {
                    top.Add((prediction.Intent, confidence));
                }
            }

            return (prediction.Intent ?? "Unknown", confidence, top);
        }
    }

    public ChatbotModelInfo GetModelInfo() => new()
    {
        Algorithm = "ML.NET SDCA Maximum Entropy (multiclass)",
        FeaturePipeline = "MapValueToKey → FeaturizeText (bag-of-words / n-grams) → SDCA",
        TrainingExampleCount = _trainingExampleCount,
        IntentCount = _labelOrder.Length,
        Intents = _labelOrder.ToList(),
        HoldoutAccuracy = Math.Round(_holdoutAccuracy, 3),
        TrainedAtUtc = DateTime.UtcNow, // process start effectively
        Notes = "Self-authored training corpus for EquaMeridian domain (bookings, invoices, VAT, disputes, listings). No external LLM."
    };

    public static class Intents
    {
        public const string Greeting = "Greeting";
        public const string Goodbye = "Goodbye";
        public const string SmallTalkThanks = "SmallTalkThanks";
        public const string BookingHelp = "BookingHelp";
        public const string BookingStatus = "BookingStatus";
        public const string InvoiceHelp = "InvoiceHelp";
        public const string InvoiceStatus = "InvoiceStatus";
        public const string VatQuestion = "VatQuestion";
        public const string DisputeHelp = "DisputeHelp";
        public const string QuotationHelp = "QuotationHelp";
        public const string QuotationStatus = "QuotationStatus";
        public const string InspectionStatus = "InspectionStatus";
        public const string ReviewHelp = "ReviewHelp";
        public const string PaymentHelp = "PaymentHelp";
        public const string RefundPolicy = "RefundPolicy";
        public const string PasswordHelp = "PasswordHelp";
        public const string ContactSupport = "ContactSupport";
        public const string ListingHelp = "ListingHelp";
        public const string DeliveryHelp = "DeliveryHelp";
        public const string LeaseHelp = "LeaseHelp";
        public const string PromoHelp = "PromoHelp";
        public const string AccountHelp = "AccountHelp";
        public const string PayoutHelp = "PayoutHelp";
        public const string CompareQuotes = "CompareQuotes";
        public const string SafetyInsurance = "SafetyInsurance";
        public const string PlatformOverview = "PlatformOverview";
    }

    private static List<ChatbotTrainingExample> BuildTrainingSet()
    {
        var set = new List<ChatbotTrainingExample>();
        void Add(string label, params string[] examples)
        {
            foreach (var e in examples)
                set.Add(new ChatbotTrainingExample { Text = e, Label = label });
        }

        Add(Intents.Greeting,
            "hi", "hello", "hey there", "good morning", "good afternoon", "hiya",
            "is anyone there", "hello bot", "hey", "howzit", "hi assistant", "yo",
            "good evening", "morning", "afternoon", "hello equameridian");

        Add(Intents.Goodbye,
            "bye", "goodbye", "see you later", "thanks bye", "that's all thanks", "ok thanks bye",
            "talk later", "done for now", "cya", "no that's all", "exit", "close chat");

        Add(Intents.SmallTalkThanks,
            "thanks", "thank you", "thanks a lot", "appreciate it", "that helped thanks",
            "great thanks", "perfect thank you", "cool thanks", "nice one thanks", "thanks for the help",
            "awesome thanks", "much appreciated");

        Add(Intents.BookingHelp,
            "how do bookings work", "how do I book machinery", "how do I rent equipment",
            "explain the booking process", "how does renting work on this platform",
            "what happens after I book a listing", "how do I book a supplier's listing",
            "how do contractors book equipment", "steps to make a booking",
            "how to hire a machine", "booking process explained", "can I reserve equipment",
            "how does the hire process work", "guide me through booking");

        Add(Intents.BookingStatus,
            "what is the status of my booking", "where is my booking", "booking status",
            "has my booking been confirmed", "is my hire active", "track my booking",
            "check booking progress", "my latest booking", "booking update",
            "has the supplier confirmed", "when does my rental start",
            "status of booking", "show my booking");

        Add(Intents.InvoiceHelp,
            "how do invoices work", "explain invoices", "what is on my invoice",
            "how is the invoice calculated", "invoice breakdown", "platform fee on invoice",
            "where do invoices appear", "when is an invoice created");

        Add(Intents.InvoiceStatus,
            "where is my invoice", "invoice status", "has my invoice been paid",
            "outstanding invoice", "latest invoice", "check my invoice",
            "do I have unpaid invoices", "show invoice");

        Add(Intents.VatQuestion,
            "how is vat calculated", "what is the vat rate", "vat on hire",
            "does the price include vat", "vat question", "tax on invoice",
            "is vat included", "vat percentage");

        Add(Intents.DisputeHelp,
            "how do I raise a dispute", "open a dispute", "machine was damaged",
            "dispute process", "problem with booking", "file a complaint",
            "equipment not as described", "raise dispute on booking");

        Add(Intents.QuotationHelp,
            "how do quotations work", "request a quote", "how to get a quotation",
            "supplier quote process", "send me a quote", "quotation request help",
            "difference between quote and booking");

        Add(Intents.QuotationStatus,
            "status of my quotation", "has the supplier replied to my quote",
            "my quote status", "pending quotation", "check quotation",
            "did I get a quote back", "quotation update");

        Add(Intents.InspectionStatus,
            "inspection status", "when is the inspection", "inspection outcome",
            "pre hire inspection", "condition inspection", "check inspection");

        Add(Intents.ReviewHelp,
            "how do I leave a review", "rate a supplier", "write a review",
            "review after hire", "can I review the machine", "feedback on booking");

        Add(Intents.PaymentHelp,
            "how do I pay", "payment methods", "payfast payment", "how to pay invoice",
            "when do I pay", "payment process", "card payment", "pay for booking");

        Add(Intents.RefundPolicy,
            "refund policy", "can I get a refund", "how refunds work",
            "cancel and refund", "deposit refund", "money back");

        Add(Intents.PasswordHelp,
            "i forgot my password", "reset password", "change password",
            "can't log in", "password help", "locked out of account");

        Add(Intents.ContactSupport,
            "contact support", "speak to a human", "help desk", "customer service",
            "email support", "phone support", "who can I contact");

        Add(Intents.ListingHelp,
            "how do I create a listing", "list my equipment", "add machinery",
            "supplier listing help", "photos for listing", "submit listing for review",
            "how to publish equipment");

        Add(Intents.DeliveryHelp,
            "how does delivery work", "pickup or delivery", "ready for pickup",
            "confirm delivery", "collected vs delivered", "delivery status",
            "mark ready for collection", "handover process");

        Add(Intents.LeaseHelp,
            "master lease agreement", "lease agreement", "sign the lease",
            "what is the mla", "rental contract", "lease terms");

        Add(Intents.PromoHelp,
            "promo code", "discount code", "how do promotions work",
            "campaign discount", "apply coupon", "special offer");

        Add(Intents.AccountHelp,
            "my account status", "verify my documents", "account not active",
            "profile settings", "deactivate account", "update my profile");

        Add(Intents.PayoutHelp,
            "when do I get paid", "supplier payout", "payout status",
            "earnings", "how payouts work", "withdraw earnings");

        Add(Intents.CompareQuotes,
            "compare quotations", "compare quotes", "which quote is better",
            "side by side quotes", "best quotation");

        Add(Intents.SafetyInsurance,
            "insurance cover", "damage waiver", "who is liable for damage",
            "safety requirements", "is equipment insured");

        Add(Intents.PlatformOverview,
            "what is equameridian", "how does this platform work", "about equameridian",
            "who is this for", "explain the marketplace", "what can I do here");

        return set;
    }
}

