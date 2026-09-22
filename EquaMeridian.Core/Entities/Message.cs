public class Message
{
    public int MessageID { get; set; }
    public int ThreadID { get; set; }
    public MessageThread Thread { get; set; } = null!;

    public int SenderID { get; set; }
    public User Sender { get; set; } = null!;

    public int RecipientID { get; set; }
    public User Recipient { get; set; } = null!;

    public string Body { get; set; } = string.Empty;
    public string? AttachmentURL { get; set; }
    public string? AttachmentFileName { get; set; }

    public bool IsRead { get; set; } = false;
    public DateTime DateSent { get; set; } = AppTime.Now;
}