using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Http;


public class TwilioSmsService : ISmsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;

    public TwilioSmsService(IHttpClientFactory httpClientFactory, IAuditService audit, IConfiguration config)
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

        var accountSid = _config["Sms:TwilioAccountSid"];
        var authToken = _config["Sms:TwilioAuthToken"];
        var fromNumber = _config["Sms:TwilioFromNumber"];

        if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken) || string.IsNullOrWhiteSpace(fromNumber))
        {
            Console.WriteLine($"[SMS] (no Twilio credentials configured) To: {toPhoneNumber} | Message: {message}");
            return;
        }

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["To"] = toPhoneNumber,
                        ["From"] = fromNumber,
                        ["Body"] = message
                    })
                };
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}")));

                var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException($"Twilio returned {response.StatusCode}: {body}");
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
