using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Http;

public class PaymentGatewayService : IPaymentGatewayService
{

    private static readonly string[] CheckoutFieldOrder =
    {
        "merchant_id", "merchant_key", "return_url", "cancel_url", "notify_url",
        "name_first", "name_last", "email_address", "cell_number",
        "m_payment_id", "amount", "item_name", "item_description",
        "custom_int1", "custom_int2", "custom_int3", "custom_int4", "custom_int5",
        "custom_str1", "custom_str2", "custom_str3", "custom_str4", "custom_str5",
        "email_confirmation", "confirmation_address"
    };


    // https://developers.payfast.co.za/docs#step_3_confirm_payment
    private static readonly string[] TrustedNotificationHosts =
    {
        "www.payfast.co.za", "sandbox.payfast.co.za", "w1w.payfast.co.za", "w2w.payfast.co.za"
    };

    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PaymentGatewayService> _logger;

    public PaymentGatewayService(
        IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<PaymentGatewayService> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private string MerchantId => _config["PayFast:MerchantId"] ?? string.Empty;
    private string MerchantKey => _config["PayFast:MerchantKey"] ?? string.Empty;
    private string? Passphrase =>
        string.IsNullOrWhiteSpace(_config["PayFast:Passphrase"]) ? null : _config["PayFast:Passphrase"];
    private bool IsSandbox => bool.TryParse(_config["PayFast:Sandbox"], out var s) ? s : true;

    private string ProcessUrl => IsSandbox
        ? "https://sandbox.payfast.co.za/eng/process"
        : "https://www.payfast.co.za/eng/process";

    private string ValidateUrl => IsSandbox
        ? "https://sandbox.payfast.co.za/eng/query/validate"
        : "https://www.payfast.co.za/eng/query/validate";

    public PaymentInitiationResult CreatePaymentRequest(
        Invoice invoice, string buyerFirstName, string buyerLastName, string buyerEmail)
    {
        if (string.IsNullOrWhiteSpace(MerchantId) || string.IsNullOrWhiteSpace(MerchantKey))
        {
            _logger.LogWarning(
                "PayFast:MerchantId/MerchantKey are not configured — would have initiated a payment " +
                "for invoice #{InvoiceNumber}, amount {Amount} ZAR.",
                invoice.InvoiceNumber, invoice.TotalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            return new PaymentInitiationResult { ProcessUrl = string.Empty, Fields = new() };
        }

        var returnUrl = _config["PayFast:ReturnUrl"] ?? $"{_config["App:FrontendBaseUrl"]}/payments/success";
        var cancelUrl = _config["PayFast:CancelUrl"] ?? $"{_config["App:FrontendBaseUrl"]}/payments/cancelled";
        var notifyUrl = _config["PayFast:NotifyUrl"] ?? throw new InvalidOperationException(
            "PayFast:NotifyUrl must be configured — PayFast needs a publicly reachable URL to POST ITNs to.");

        var fields = new Dictionary<string, string>
        {
            ["merchant_id"] = MerchantId,
            ["merchant_key"] = MerchantKey,
            ["return_url"] = returnUrl,
            ["cancel_url"] = cancelUrl,
            ["notify_url"] = notifyUrl,
            ["name_first"] = buyerFirstName,
            ["name_last"] = buyerLastName,
            ["email_address"] = buyerEmail,
            ["m_payment_id"] = invoice.InvoiceNumber,
            ["amount"] = invoice.TotalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            ["item_name"] = $"EquaMeridian Invoice {invoice.InvoiceNumber}",
            ["item_description"] = $"Booking-linked invoice #{invoice.InvoiceID} — {invoice.Currency} {invoice.TotalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}",
            ["custom_int1"] = invoice.InvoiceID.ToString()
        };

        fields["signature"] = GenerateSignature(fields, Passphrase);

        return new PaymentInitiationResult { ProcessUrl = ProcessUrl, Fields = fields };
    }

    public bool VerifyItnSignature(string rawBody, string? receivedSignature)
    {
        if (string.IsNullOrWhiteSpace(receivedSignature)) return false;
        if (string.IsNullOrWhiteSpace(rawBody)) return false;
        var pairs = rawBody
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair => !pair.StartsWith("signature=", StringComparison.Ordinal));
        var paramString = string.Join("&", pairs);

        if (!string.IsNullOrWhiteSpace(Passphrase))
            paramString += "&passphrase=" + PhpStyleUrlEncode(Passphrase);

        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(paramString));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();

        var matched = string.Equals(expected, receivedSignature, StringComparison.OrdinalIgnoreCase);
        if (!matched)
        {
            _logger.LogWarning("PayFast ITN signature verification failed.");
        }

        return matched;
    }

    public async Task<bool> IsTrustedPayFastSourceAsync(string? remoteIp)
    {
        if (string.IsNullOrWhiteSpace(remoteIp)) return false;
        if (!IPAddress.TryParse(remoteIp, out var callerIp)) return false;

        foreach (var host in TrustedNotificationHosts)
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(host);
                if (addresses.Any(a => a.Equals(callerIp)))
                    return true;
            }
            catch
            {
            }
        }
        return false;
    }

    public async Task<bool> ConfirmWithPayFastAsync(IDictionary<string, string> postedFields)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var content = new FormUrlEncodedContent(postedFields);
            var response = await client.PostAsync(ValidateUrl, content);
            if (!response.IsSuccessStatusCode) return false;

            var body = await response.Content.ReadAsStringAsync();
            return body.Trim().Equals("VALID", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayFast ITN confirmation call to {ValidateUrl} failed.", ValidateUrl);
            return false;
        }
    }

    /// <summary>
    /// Actively queries PayFast for the current status of a transaction, rather than relying purely on the
    /// asynchronous ITN webhook. This closes the gap where an invoice could sit on "Pending" indefinitely
    /// if PayFast's callback to /payfast/notify never arrived (e.g. blocked, delayed, or missed in a
    /// non-publicly-reachable dev environment) even though the payment itself succeeded on PayFast's side.
    /// </summary>
    public async Task<PaymentGatewaySyncResult> GetTransactionStatusAsync(string gatewayReference)
    {
        if (string.IsNullOrWhiteSpace(gatewayReference) ||
            string.IsNullOrWhiteSpace(MerchantId) || string.IsNullOrWhiteSpace(MerchantKey))
        {
            return new PaymentGatewaySyncResult { Success = false, Status = null };
        }

        try
        {
            var timestamp = AppTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
            var signedFields = new Dictionary<string, string>
            {
                ["merchant-id"] = MerchantId,
                ["version"] = "v1",
                ["timestamp"] = timestamp
            };
            var signature = GenerateSignature(signedFields, Passphrase);

            var host = IsSandbox ? "https://api.payfast.co.za" : "https://api.payfast.co.za";
            var requestUri = $"{host}/transactions/{Uri.EscapeDataString(gatewayReference)}/fetch" +
                              $"?testing={(IsSandbox ? "true" : "false")}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("merchant-id", MerchantId);
            request.Headers.Add("version", "v1");
            request.Headers.Add("timestamp", timestamp);
            request.Headers.Add("signature", signature);

            var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "PayFast transaction status query for {Reference} returned {StatusCode}.",
                    gatewayReference, response.StatusCode);
                return new PaymentGatewaySyncResult { Success = false, Status = null };
            }

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var payload = root.TryGetProperty("data", out var dataEl) ? dataEl : root;

            string? rawStatus = payload.TryGetProperty("status", out var statusEl) ? statusEl.GetString()
                : payload.TryGetProperty("payment_status", out var altStatusEl) ? altStatusEl.GetString()
                : null;

            var mappedStatus = rawStatus?.Trim().ToUpperInvariant() switch
            {
                "COMPLETE" => "Paid",
                "COMPLETED" => "Paid",
                "FAILED" => "Failed",
                "CANCELLED" => "Cancelled",
                _ => (string?)null
            };

            return new PaymentGatewaySyncResult { Success = mappedStatus != null, Status = mappedStatus };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayFast transaction status query for {Reference} failed.", gatewayReference);
            return new PaymentGatewaySyncResult { Success = false, Status = null };
        }
    }

    public string GenerateSignature(IEnumerable<KeyValuePair<string, string>> fields, string? passphrase)
    {
        var ordered = OrderForSigning(fields);

        var sb = new StringBuilder();
        foreach (var (key, value) in ordered)
        {
            // PayFast's own reference signature implementation trims every value before
            // encoding it (urlencode(trim($val))) — mirror that here so a stray leading/
            // trailing space on any field (e.g. a name with double-spacing) can't silently
            // produce a signature that PayFast's trimmed recomputation won't match.
            var trimmed = value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmed)) continue;
            if (sb.Length > 0) sb.Append('&');
            sb.Append(key).Append('=').Append(PhpStyleUrlEncode(trimmed));
        }

        if (!string.IsNullOrWhiteSpace(passphrase))
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append("passphrase=").Append(PhpStyleUrlEncode(passphrase));
        }

        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static IEnumerable<KeyValuePair<string, string>> OrderForSigning(
        IEnumerable<KeyValuePair<string, string>> fields)
    {
        var dict = fields.ToDictionary(kv => kv.Key, kv => kv.Value);
        var known = CheckoutFieldOrder.Where(dict.ContainsKey).Select(k => new KeyValuePair<string, string>(k, dict[k]));
        var extra = dict.Keys.Except(CheckoutFieldOrder).Select(k => new KeyValuePair<string, string>(k, dict[k]));
        return known.Concat(extra);
    }
    private static string PhpStyleUrlEncode(string value)
    {
        var escaped = Uri.EscapeDataString(value);
        var sb = new StringBuilder(escaped.Length);
        foreach (var ch in escaped)
        {
            if (ch == '%') { sb.Append(ch); continue; }
            sb.Append(ch);
        }
        var result = sb.ToString().Replace("%20", "+");
        result = System.Text.RegularExpressions.Regex.Replace(
            result, "%[0-9a-f]{2}", m => m.Value.ToUpperInvariant());
        return result;
    }

    public async Task<PaymentGatewayRefundResult> RequestRefundAsync(
        string gatewayReference, decimal amount, string reason, string? merchantRefundId = null)
    {
        if (string.IsNullOrWhiteSpace(gatewayReference))
            return new PaymentGatewayRefundResult { Success = false, Message = "Missing PayFast payment reference." };

        if (string.IsNullOrWhiteSpace(MerchantId) || string.IsNullOrWhiteSpace(MerchantKey))
        {
            _logger.LogWarning(
                "PayFast refund skipped — MerchantId/MerchantKey not configured. Reference {Ref}, amount {Amount}.",
                gatewayReference, amount);
            // Allow local/dev processing so admin workflow is not blocked without credentials.
            return new PaymentGatewayRefundResult
            {
                Success = true,
                Message = "PayFast not configured; refund recorded locally only.",
                GatewayRefundId = null
            };
        }

        try
        {
            // PayFast Refunds API (merchant API). Sandbox support varies by account.
            var baseUrl = IsSandbox
                ? "https://sandbox.payfast.co.za/eng/refund"
                : "https://api.payfast.co.za/refunds";

            var client = _httpClientFactory.CreateClient();
            var payload = new Dictionary<string, string>
            {
                ["merchant_id"] = MerchantId,
                ["merchant_key"] = MerchantKey,
                ["pf_payment_id"] = gatewayReference,
                ["amount"] = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                ["reason"] = reason ?? "Refund approved by administrator"
            };
            if (!string.IsNullOrWhiteSpace(merchantRefundId))
                payload["m_payment_id"] = merchantRefundId;
            if (!string.IsNullOrWhiteSpace(Passphrase))
                payload["signature"] = GenerateSignature(payload, Passphrase);

            using var content = new FormUrlEncodedContent(payload);
            var response = await client.PostAsync(baseUrl, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "PayFast refund failed for {Ref}: {Status} {Body}",
                    gatewayReference, (int)response.StatusCode, body);
                return new PaymentGatewayRefundResult
                {
                    Success = false,
                    Message = $"PayFast refund rejected ({(int)response.StatusCode}). {Truncate(body, 200)}"
                };
            }

            _logger.LogInformation("PayFast refund accepted for {Ref}. Body: {Body}", gatewayReference, Truncate(body, 300));
            return new PaymentGatewayRefundResult
            {
                Success = true,
                Message = "Refund submitted to PayFast.",
                GatewayRefundId = gatewayReference
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayFast refund request failed for {Ref}.", gatewayReference);
            return new PaymentGatewayRefundResult { Success = false, Message = ex.Message };
        }
    }

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "…");

}
