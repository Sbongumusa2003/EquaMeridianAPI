using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

// Swapped from Twilio to ClickSend per the lecturer's note. Same ISmsService contract, so nothing
// downstream (QuoteExpiryTimerService, AuthService, etc.) needed to change — only Program.cs's DI
// registration and the Sms: config keys in appsettings.json.
public class ClickSendSmsService : ISmsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;

    public ClickSendSmsService(IHttpClientFactory httpClientFactory, IAuditService audit, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _audit = audit;
        _config = config;
    }

    public Task SendQuotationExpiredSmsAsync(string? toPhoneNumber, string name, int quotationId, string listingTitle)
        => SendSmsAsync(toPhoneNumber,
            $"Hi {name}, your EquaMeridian quote #{quotationId} for \"{listingTitle}\" has expired. " +
            "Log in to request a new quote.");

    public Task SendBookingConfirmedSmsAsync(string? toPhoneNumber, string name, int bookingId, string listingTitle)
        => SendSmsAsync(toPhoneNumber,
            $"Hi {name}, your EquaMeridian booking #{bookingId} for \"{listingTitle}\" is confirmed.");

    public async Task SendSmsAsync(string? toPhoneNumber, string message)
    {
        if (string.IsNullOrWhiteSpace(toPhoneNumber))
        {
            Console.WriteLine($"[SMS] (no phone number on file) Message: {message}");
            return;
        }

        var username = _config["Sms:ClickSendUsername"];
        var apiKey = _config["Sms:ClickSendApiKey"];
        var senderId = _config["Sms:ClickSendSenderId"]; // optional custom "from" name/number

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine($"[SMS] (no ClickSend credentials configured) To: {toPhoneNumber} | Message: {message}");
            return;
        }

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                const string url = "https://rest.clicksend.com/v3/sms/send";

                var payload = new
                {
                    messages = new[]
                    {
                        new
                        {
                            to = toPhoneNumber,
                            body = message,
                            source = "EquaMeridian",
                            from = string.IsNullOrWhiteSpace(senderId) ? null : senderId
                        }
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{apiKey}")));

                var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"ClickSend returned {response.StatusCode}: {body}");
                }
                return;
            }
            catch when (attempt < 3)
            {
                await Task.Delay(500 * attempt);
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(null, "NOTIFICATION_DISPATCH_FAILED",
                    $"SMS to {toPhoneNumber} failed after 3 attempts: {ex.Message}", null, null, null, null);
            }
        }
    }
}
