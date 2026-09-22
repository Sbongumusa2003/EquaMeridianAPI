using EquaMeridian.DTOs.Chatbot;

public interface IChatbotService
{
    Task<ChatbotReplyDto> AskAsync(string message, int? userId, string? role, string? sessionId = null);
    ChatbotModelInfo GetModelInfo();
    void RecordFeedback(ChatbotFeedbackDto dto);
    IReadOnlyList<object> GetFeedbackSummary();
}
