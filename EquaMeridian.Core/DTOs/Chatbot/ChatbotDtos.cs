using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Chatbot
{
    public class ChatbotAskDto
    {
        [Required, StringLength(500, MinimumLength = 1)]
        public string Message { get; set; } = string.Empty;

        /// <summary>Optional client session id for multi-turn context (anonymous users).</summary>
        [StringLength(64)]
        public string? SessionId { get; set; }
    }

    public class ChatbotReplyDto
    {
        public string Reply { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public List<string> QuickReplies { get; set; } = new();
        public List<ChatbotIntentScoreDto> TopIntents { get; set; } = new();
        public string? SessionId { get; set; }
        public ChatbotEntitiesDto? Entities { get; set; }
    }

    public class ChatbotIntentScoreDto
    {
        public string Intent { get; set; } = string.Empty;
        public float Score { get; set; }
    }

    public class ChatbotEntitiesDto
    {
        public int? BookingId { get; set; }
        public string? InvoiceNumber { get; set; }
    }

    public class ChatbotFeedbackDto
    {
        [StringLength(64)]
        public string? SessionId { get; set; }

        [StringLength(80)]
        public string? Intent { get; set; }

        public bool Helpful { get; set; }

        [StringLength(300)]
        public string? Comment { get; set; }
    }

    public class ChatbotModelInfo
    {
        public string Algorithm { get; set; } = string.Empty;
        public string FeaturePipeline { get; set; } = string.Empty;
        public int TrainingExampleCount { get; set; }
        public int IntentCount { get; set; }
        public List<string> Intents { get; set; } = new();
        public double HoldoutAccuracy { get; set; }
        public DateTime TrainedAtUtc { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
