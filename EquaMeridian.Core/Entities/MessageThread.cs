public class MessageThread
{
    public int ThreadID { get; set; }
    public int ParticipantOneID { get; set; }
    public User ParticipantOne { get; set; } = null!;

    public int ParticipantTwoID { get; set; }
    public User ParticipantTwo { get; set; } = null!;

    public DateTime CreatedDate { get; set; } = AppTime.Now;
    public DateTime LastActivityDate { get; set; } = AppTime.Now;

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}