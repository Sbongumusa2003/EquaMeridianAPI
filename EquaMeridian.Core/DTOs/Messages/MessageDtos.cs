using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Messages
{
    public class SendMessageDto
    {
        [Required(ErrorMessage = "This field is required.")]
        public int RecipientID { get; set; }

        [Required(ErrorMessage = "This field is required.")]
        [MinLength(1, ErrorMessage = "This field is required.")]
        public string Body { get; set; } = string.Empty;
    }
    public class ReplyMessageDto
    {
        [Required(ErrorMessage = "This field is required.")]
        [MinLength(1, ErrorMessage = "This field is required.")]
        public string Body { get; set; } = string.Empty;
    }

    public class MessageDto
    {
        public int MessageID { get; set; }
        public int ThreadID { get; set; }
        public int SenderID { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public int RecipientID { get; set; }
        public string RecipientName { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? AttachmentURL { get; set; }
        public string? AttachmentFileName { get; set; }
        public bool IsRead { get; set; }
        public DateTime DateSent { get; set; }
    }
    public class ThreadListItemDto
    {
        public int ThreadID { get; set; }
        public int OtherParticipantID { get; set; }
        public string OtherParticipantName { get; set; } = string.Empty;
        public string MessagePreview { get; set; } = string.Empty;
        public DateTime LastActivityDate { get; set; }
        public int UnreadCount { get; set; }
    }

    public class ThreadDetailDto
    {
        public int ThreadID { get; set; }
        public int OtherParticipantID { get; set; }
        public string OtherParticipantName { get; set; } = string.Empty;
        public List<MessageDto> Messages { get; set; } = new();
    }

    public class RecipientSearchResultDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
    }
}