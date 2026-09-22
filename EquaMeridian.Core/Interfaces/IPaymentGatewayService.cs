
/// documented no-op for now; see <see cref="IPaymentGatewayService.GetTransactionStatusAsync"/>.

public class PaymentGatewaySyncResult
{
    public bool Success { get; set; }
    public string? Status { get; set; }
}

public class PaymentInitiationResult
{
    public string ProcessUrl { get; set; } = string.Empty;
    public Dictionary<string, string> Fields { get; set; } = new();
}

public interface IPaymentGatewayService
{
    PaymentInitiationResult CreatePaymentRequest(
        Invoice invoice, string buyerFirstName, string buyerLastName, string buyerEmail);

    bool VerifyItnSignature(string rawBody, string? receivedSignature);

    Task<bool> IsTrustedPayFastSourceAsync(string? remoteIp);

    Task<bool> ConfirmWithPayFastAsync(IDictionary<string, string> postedFields);

    Task<PaymentGatewaySyncResult> GetTransactionStatusAsync(string gatewayReference);

    Task<PaymentGatewayRefundResult> RequestRefundAsync(
        string gatewayReference, decimal amount, string reason, string? merchantRefundId = null);
}

public class PaymentGatewayRefundResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? GatewayRefundId { get; set; }
}