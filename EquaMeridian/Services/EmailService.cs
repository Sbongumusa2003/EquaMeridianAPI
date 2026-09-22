using SendGrid;
using SendGrid.Helpers.Mail;

public class EmailService : IEmailService
{
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public EmailService(IAuditService audit, IConfiguration config)
    {
        _audit = audit;
        _config = config;
        _fromEmail = _config["Email:FromAddress"] ?? "noreply@equameridian.co.za";
        _fromName = _config["Email:FromName"] ?? "EquaMeridian";
    }

    public Task SendLockoutEmailAsync(string email, string name)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: "Your EquaMeridian account has been locked",
                body: $"Hi {name},<br><br>"
                    + "Your account has been locked after 5 failed login attempts.<br>"
                    + "It will automatically unlock after 30 minutes.<br><br>"
                    + "If this was not you, please contact support immediately."
            ),
            auditType: "SendLockoutEmail"
        );

    public Task SendPasswordResetEmailAsync(string email, string name, string resetUrl)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: "Reset your EquaMeridian password",
                body: $"Hi {name},<br><br>"
                    + "Click the link below to reset your password. "
                    + "This link expires in 24 hours.<br><br>"
                    + $"<a href=\"{resetUrl}\">Reset Password</a><br><br>"
                    + "If you did not request this, you can safely ignore this email."
            ),
            auditType: "SendPasswordResetEmail"
        );

    public Task SendNewMessageEmailAsync(string email, string name, string senderName, int threadId)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"New message from {senderName}",
                body: $"Hi {name},<br><br>"
                    + $"You have received a new message from <strong>{senderName}</strong> "
                    + $"in conversation #{threadId}.<br><br>"
                    + "Log in to your account to read and reply."
            ),
            auditType: "SendNewMessageEmail"
        );

    public Task SendInternalAccountCreatedEmailAsync(string email, string name, string role, string temporaryPassword)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: "Your EquaMeridian staff account has been created",
                body: $"Hi {name},<br><br>"
                    + $"An administrator has created a <strong>{role}</strong> account for you on EquaMeridian.<br><br>"
                    + $"Your temporary password is: <strong>{temporaryPassword}</strong><br><br>"
                    + "Log in with the email address this was sent to and the temporary password above. "
                    + "For security, please change your password immediately after your first login."
            ),
            auditType: "SendInternalAccountCreatedEmail"
        );

    public Task SendQuotationExpiredEmailAsync(string email, string name, int quotationId, string listingTitle)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Your quote #{quotationId} has expired",
                body: $"Hi {name},<br><br>"
                    + $"Your quotation #{quotationId} for <strong>{listingTitle}</strong> has expired "
                    + "without being accepted.<br><br>"
                    + "Log in to your account if you'd like to request a new quote."
            ),
            auditType: "SendQuotationExpiredEmail"
        );

    public Task SendOtpCodeEmailAsync(string email, string name, string code)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: "Your EquaMeridian verification code",
                body: $"Hi {name},<br><br>"
                    + $"Your one-time verification code is: <strong style=\"font-size:20px\">{code}</strong><br><br>"
                    + "This code expires in 10 minutes. If you didn't try to log in, you can ignore this email."
            ),
            auditType: "SendOtpCodeEmail"
        );

    public Task SendPaymentReceivedEmailAsync(string email, string name, int bookingId, string invoiceNumber, decimal supplierPayableAmount)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Payment received for booking #{bookingId}",
                body: $"Hi {name},<br><br>"
                    + $"Payment for invoice <strong>{invoiceNumber}</strong> (booking #{bookingId}) has been received. "
                    + $"Your payout amount for this booking is <strong>R{supplierPayableAmount:N2}</strong>.<br><br>"
                    + "You can now arrange delivery or prepare the machinery for pickup — log in to your account to mark it ready."
            ),
            auditType: "SendPaymentReceivedEmail"
        );

    public Task SendBookingCancelledEmailAsync(string email, string name, int bookingId, string listingTitle, string cancelledByRole, string? reason)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Booking #{bookingId} has been cancelled",
                body: $"Hi {name},<br><br>"
                    + $"Booking <strong>#{bookingId}</strong> for <strong>{listingTitle}</strong> has been cancelled by the {cancelledByRole.ToLower()}."
                    + (string.IsNullOrWhiteSpace(reason) ? "" : $"<br><br>Reason given: <em>{reason}</em>")
                    + "<br><br>Log in to your account for more details."
            ),
            auditType: "SendBookingCancelledEmail"
        );

    public Task SendPasswordChangedNotificationAsync(string email, string name)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: "Your EquaMeridian password was changed",
                body: $"Hi {name},<br><br>"
                    + "Your password was successfully changed.<br>"
                    + "If you did not make this change, please contact support immediately."
            ),
            auditType: "SendPasswordChangedNotification"
        );

    public Task SendAccountStatusChangedAsync(string email, string name, string newStatus)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: "Your EquaMeridian account status has changed",
                body: $"Hi {name},<br><br>"
                    + $"Your account status has been updated to: <strong>{newStatus}</strong>.<br><br>"
                    + "If you have any questions, please contact support."
            ),
            auditType: "SendAccountStatusChanged"
        );

    public Task SendListingStatusChangedAsync(string email, string name,
        int listingId, string newStatus, string? reason)
    {
        var reasonPart = !string.IsNullOrWhiteSpace(reason)
            ? $"<br><br>Reason: {reason}"
            : string.Empty;

        return SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Your listing #{listingId} status has been updated",
                body: $"Hi {name},<br><br>"
                    + $"The status of your listing <strong>#{listingId}</strong> "
                    + $"has been changed to: <strong>{newStatus}</strong>."
                    + $"{reasonPart}<br><br>"
                    + "Log in to your supplier dashboard to view the listing."
            ),
            auditType: "SendListingStatusChanged"
        );
    }

    public Task SendNewListingPendingReviewAsync(string adminEmail, int listingId, string supplierName)
        => SendWithRetryAsync(
            () => SendAsync(
                to: adminEmail,
                toName: "Admin",
                subject: $"New listing #{listingId} pending review",
                body: $"A new listing (ID: <strong>{listingId}</strong>) was submitted by "
                    + $"<strong>{supplierName}</strong> and is awaiting your review.<br><br>"
                    + "Log in to the admin panel to approve or reject."
            ),
            auditType: "SendNewListingPendingReview"
        );

    public Task SendNewSupplierPendingReviewAsync(string adminEmail, int userId, string supplierName, string companyName)
        => SendWithRetryAsync(
            () => SendAsync(
                to: adminEmail,
                toName: "Admin",
                subject: $"New supplier registration #{userId} pending review",
                body: $"A new Supplier account (ID: <strong>{userId}</strong>) was registered by "
                    + $"<strong>{supplierName}</strong> ({companyName}) and is awaiting document review "
                    + "before it can be activated.<br><br>"
                    + "Log in to the admin panel to review their documents and approve or reject the account."
            ),
            auditType: "SendNewSupplierPendingReview"
        );

    public Task SendDisputeResolutionEmailAsync(string email, string name, int disputeId,
        string resolutionOutcome, string resolutionNotes)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Dispute #{disputeId} has been resolved",
                body: $"Hi {name},<br><br>"
                    + $"Dispute <strong>#{disputeId}</strong> has been reviewed and resolved "
                    + $"with the outcome: <strong>{resolutionOutcome}</strong>.<br><br>"
                    + $"Resolution notes: {resolutionNotes}<br><br>"
                    + "Log in to your account for more details."
            ),
            auditType: "SendDisputeResolutionEmail"
        );

    public Task SendQuotationSubmittedEmailAsync(string email, string name, int quotationId, string listingTitle)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"New quotation received for {listingTitle}",
                body: $"Hi {name},<br><br>"
                    + $"You have received a new quotation (<strong>#{quotationId}</strong>) "
                    + $"for <strong>{listingTitle}</strong>.<br><br>"
                    + "Log in to your account to review the quotation."
            ),
            auditType: "SendQuotationSubmittedEmail"
        );

    public Task SendDocumentReviewedEmailAsync(string email, string name, int docId, string docName, string decision, string? reason = null)
    {
        var isRejected = decision.Equals("Rejected", StringComparison.OrdinalIgnoreCase);
        var body = $"Hi {name},<br><br>"
            + $"Your document <strong>'{docName}'</strong> (ID: <strong>{docId}</strong>) "
            + $"has been <strong>{decision.ToLowerInvariant()}</strong>.<br><br>";

        if (isRejected)
        {
            if (!string.IsNullOrWhiteSpace(reason))
                body += $"Reason: {reason}<br><br>";
            body += "Please log in and re-upload the correct document from your Documents section "
                  + "as soon as possible so your account review can continue.";
        }
        else
        {
            body += "Log in to your account for more details.";
        }

        return SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Your document '{docName}' has been {decision.ToLowerInvariant()}",
                body: body
            ),
            auditType: "SendDocumentReviewedEmail"
        );
    }

    public Task SendInspectionRequestedEmailAsync(string email, string name, int inspectionId,
        string listingTitle, DateTime scheduledDate)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Inspection requested for {listingTitle}",
                body: $"Hi {name},<br><br>"
                    + $"An inspection (<strong>#{inspectionId}</strong>) has been requested for your machinery "
                    + $"<strong>{listingTitle}</strong>, scheduled for <strong>{scheduledDate:d MMMM yyyy}</strong>.<br><br>"
                    + "Log in to your account for more details."
            ),
            auditType: "SendInspectionRequestedEmail"
        );

    public Task SendInspectionRequestedAdminEmailAsync(string adminEmail, int inspectionId,
        string listingTitle, DateTime scheduledDate, string requesterName)
        => SendWithRetryAsync(
            () => SendAsync(
                to: adminEmail,
                toName: "Admin",
                subject: $"Contractor requested inspection for {listingTitle}",
                body: $"Hi Admin,<br><br>"
                    + $"<strong>{requesterName}</strong> has requested an inspection "
                    + $"(<strong>#{inspectionId}</strong>) for <strong>{listingTitle}</strong>, "
                    + $"scheduled for <strong>{scheduledDate:d MMMM yyyy}</strong>.<br><br>"
                    + "Log in to the admin panel for more details."
            ),
            auditType: "SendInspectionRequestedAdminEmail"
        );

    public Task SendInspectionOutcomeConfirmedEmailAsync(string email, string name, int inspectionId,
        string listingTitle, string outcome)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Inspection #{inspectionId} outcome confirmed",
                body: $"Hi {name},<br><br>"
                    + $"The inspection outcome for <strong>{listingTitle}</strong> "
                    + $"(<strong>#{inspectionId}</strong>) has been confirmed as: <strong>{outcome}</strong>.<br><br>"
                    + "Log in to your account for more details."
            ),
            auditType: "SendInspectionOutcomeConfirmedEmail"
        );

    public Task SendDeliveryConfirmedEmailAsync(string email, string name, int bookingId, string listingTitle)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Delivery confirmed for booking #{bookingId}",
                body: $"Hi {name},<br><br>"
                    + $"The contractor has confirmed receipt of <strong>{listingTitle}</strong> "
                    + $"for booking <strong>#{bookingId}</strong>.<br><br>"
                    + "Log in to your account for more details."
            ),
            auditType: "SendDeliveryConfirmedEmail"
        );

    public Task SendReadyForPickupEmailAsync(string email, string name, int bookingId, string listingTitle)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Your machine is ready for pickup — booking #{bookingId}",
                body: $"Hi {name},<br><br>"
                    + $"<strong>{listingTitle}</strong> is prepped and ready for collection "
                    + $"for booking <strong>#{bookingId}</strong>.<br><br>"
                    + "Log in to your account for pickup/delivery details."
            ),
            auditType: "SendReadyForPickupEmail"
        );

    public Task SendReadyForReturnPickupEmailAsync(string email, string name, int bookingId, string listingTitle)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Return pickup arranged — booking #{bookingId}",
                body: $"Hi {name},<br><br>"
                    + $"The supplier has arranged collection of <strong>{listingTitle}</strong> "
                    + $"for booking <strong>#{bookingId}</strong>.<br><br>"
                    + "Log in to your account for collection details."
            ),
            auditType: "SendReadyForReturnPickupEmail"
        );

    public Task SendQuotationRequestedEmailAsync(string email, string name, int quotationId, string listingTitle)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"New quotation request for {listingTitle}",
                body: $"Hi {name},<br><br>"
                    + $"You have received a new quotation request (<strong>#{quotationId}</strong>) "
                    + $"for <strong>{listingTitle}</strong>.<br><br>"
                    + "Log in to your account to review and submit a quote."
            ),
            auditType: "SendQuotationRequestedEmail"
        );

    public Task SendQuotationAcceptedEmailAsync(string email, string name, int quotationId, int bookingId, string listingTitle)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Quotation #{quotationId} accepted",
                body: $"Hi {name},<br><br>"
                    + $"Your quotation <strong>#{quotationId}</strong> for <strong>{listingTitle}</strong> "
                    + $"has been accepted. Booking <strong>#{bookingId}</strong> has been created.<br><br>"
                    + "Log in to your account for more details."
            ),
            auditType: "SendQuotationAcceptedEmail"
        );

    public Task SendQuotationRejectedEmailAsync(string email, string name, int quotationId, string listingTitle)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Quotation #{quotationId} declined",
                body: $"Hi {name},<br><br>"
                    + $"Your quotation <strong>#{quotationId}</strong> for <strong>{listingTitle}</strong> "
                    + "has been declined by the contractor.<br><br>"
                    + "Log in to your account for more details."
            ),
            auditType: "SendQuotationRejectedEmail"
        );

    public Task SendInvoiceGeneratedEmailAsync(string email, string name, int invoiceId, string invoiceNumber, decimal amount)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Invoice {invoiceNumber} generated",
                body: $"Hi {name},<br><br>"
                    + $"Invoice <strong>{invoiceNumber}</strong> has been generated for an amount of "
                    + $"<strong>ZAR {amount:N2}</strong>.<br><br>"
                    + "Log in to your account to view and download the invoice."
            ),
            auditType: "SendInvoiceGeneratedEmail"
        );

    public Task SendLeaseAgreementSignatureRequiredEmailAsync(string email, string name, int leaseAgreementId, string agreementNumber)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Your signature is required on lease agreement {agreementNumber}",
                body: $"Hi {name},<br><br>"
                    + $"The other party has signed lease agreement <strong>{agreementNumber}</strong>. "
                    + "Please log in to your account to review and sign the agreement."
            ),
            auditType: "SendLeaseAgreementSignatureRequiredEmail"
        );

    public Task SendLeaseAgreementFullyExecutedEmailAsync(string email, string name, int leaseAgreementId, string agreementNumber)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Lease agreement {agreementNumber} is now legally binding",
                body: $"Hi {name},<br><br>"
                    + $"Both parties have signed lease agreement <strong>{agreementNumber}</strong>. "
                    + "It is now fully executed and legally binding.<br><br>"
                    + "Log in to your account to view the signed agreement."
            ),
            auditType: "SendLeaseAgreementFullyExecutedEmail"
        );

    public Task SendReturnRequestedEmailAsync(string email, string name, int bookingId, string machinery,
        DateTime preferredPickupDate, string timeWindow, string pickupLocation, string reason)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Return requested for booking #{bookingId}",
                body: $"Hi {name},<br><br>"
                    + $"The Contractor has requested a return for <strong>{machinery}</strong> "
                    + $"(booking #{bookingId}).<br><br>"
                    + $"Reason: {reason}<br>"
                    + $"Preferred pickup date: {preferredPickupDate:yyyy-MM-dd}<br>"
                    + $"Time window: {timeWindow}<br>"
                    + $"Pickup location: {pickupLocation}<br><br>"
                    + "Please arrange collection and confirm the return once inspected."
            ),
            auditType: "SendReturnRequestedEmail"
        );

    public Task SendReturnConfirmedEmailAsync(string email, string name, int bookingId, string machinery, string condition)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Return confirmed for booking #{bookingId}",
                body: $"Hi {name},<br><br>"
                    + $"The Supplier has confirmed the return of <strong>{machinery}</strong> "
                    + $"(booking #{bookingId}). Recorded condition: <strong>{condition}</strong>.<br><br>"
                    + (condition == "Damaged"
                        ? "A damage assessment is being reviewed and may affect your deposit."
                        : "The booking is now complete.")
            ),
            auditType: "SendReturnConfirmedEmail"
        );

    public Task SendDisputeRaisedEmailAsync(string email, string name, int disputeId, int bookingId, string category)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Dispute #{disputeId} raised for booking #{bookingId}",
                body: $"Hi {name},<br><br>"
                    + $"A dispute (category: <strong>{category}</strong>) has been raised for booking #{bookingId}.<br><br>"
                    + "Our team will review it shortly."
            ),
            auditType: "SendDisputeRaisedEmail"
        );

    public Task SendReviewSubmittedEmailAsync(string email, string name, int bookingId, string machinery, int overallRating, string reviewTitle)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"New review for {machinery}",
                body: $"Hi {name},<br><br>"
                    + $"A Contractor has left a <strong>{overallRating}-star</strong> review "
                    + $"titled \"{reviewTitle}\" for booking <strong>#{bookingId}</strong> "
                    + $"({machinery}).<br><br>"
                    + "Log in to your account to view the full review."
            ),
            auditType: "SendReviewSubmittedEmail"
        );

    public Task SendReviewUpdatedEmailAsync(string email, string name, int reviewId, string machinery, int overallRating)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"Review #{reviewId} updated for {machinery}",
                body: $"Hi {name},<br><br>"
                    + $"A review (<strong>#{reviewId}</strong>) for <strong>{machinery}</strong> "
                    + $"has been updated. New rating: <strong>{overallRating}-star</strong>.<br><br>"
                    + "Log in to your account to view the updated review."
            ),
            auditType: "SendReviewUpdatedEmail"
        );

    public Task SendReviewDeletedEmailAsync(string email, string name, int reviewId, string machinery)
        => SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: name,
                subject: $"A review for {machinery} was removed",
                body: $"Hi {name},<br><br>"
                    + $"Review <strong>#{reviewId}</strong> for <strong>{machinery}</strong> "
                    + "has been removed by the Contractor who left it.<br><br>"
                    + "Log in to your account for more details."
            ),
            auditType: "SendReviewDeletedEmail"
        );

    public Task SendNotificationEmailAsync(string email, string name, string title, string body, string? type = null)
    {
        var safeName = string.IsNullOrWhiteSpace(name) ? "there" : name;
        var typeLabel = string.IsNullOrWhiteSpace(type) ? "Notification" : type;
        var inner =
            $"<p style=\"margin:0 0 16px;font-size:15px;line-height:1.55;color:#3a3630;\">Hi {System.Net.WebUtility.HtmlEncode(safeName)},</p>"
            + $"<p style=\"margin:0 0 8px;font-size:12px;letter-spacing:0.08em;text-transform:uppercase;color:#a8841a;font-weight:700;\">{System.Net.WebUtility.HtmlEncode(typeLabel)}</p>"
            + $"<h2 style=\"margin:0 0 12px;font-size:18px;line-height:1.35;color:#0e0d0b;\">{System.Net.WebUtility.HtmlEncode(title)}</h2>"
            + $"<div style=\"margin:0 0 20px;font-size:15px;line-height:1.6;color:#3a3630;\">{body}</div>"
            + BuildCtaButton("Open EquaMeridian Hub", HubUrl("/"));

        return SendWithRetryAsync(
            () => SendAsync(
                to: email,
                toName: safeName,
                subject: title,
                body: inner
            ),
            auditType: "SendNotificationEmail"
        );
    }

    private string HubUrl(string path)
    {
        var baseUrl = (_config["App:FrontendBaseUrl"] ?? "http://localhost:4200").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(path)) return baseUrl;
        return path.StartsWith('/') ? baseUrl + path : baseUrl + "/" + path;
    }

    private static string BuildCtaButton(string label, string href) =>
        "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:8px 0 4px;\">"
        + "<tr><td style=\"border-radius:8px;background:linear-gradient(135deg,#e0c36a,#a8841a);\">"
        + $"<a href=\"{href}\" style=\"display:inline-block;padding:12px 22px;font-size:14px;font-weight:700;"
        + "color:#1a1408;text-decoration:none;border-radius:8px;\">{label}</a>"
        + "</td></tr></table>";

    /// <summary>EquaMeridian branded HTML shell — charcoal header, gold accents, sand body, confidential footer.</summary>
    private string WrapHtml(string subject, string innerBody)
    {
        var year = AppTime.Now.Year;
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8""/>
<meta name=""viewport"" content=""width=device-width, initial-scale=1""/>
<title>{System.Net.WebUtility.HtmlEncode(subject)}</title>
</head>
<body style=""margin:0;padding:0;background:#f4f0e8;font-family:Arial,Helvetica,sans-serif;color:#1a1814;"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f4f0e8;padding:24px 12px;"">
<tr><td align=""center"">
<table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px;width:100%;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(14,13,11,0.08);"">
  <tr>
    <td style=""background:#0e0d0b;padding:18px 28px;border-bottom:3px solid #c9a227;"">
      <div style=""font-size:18px;font-weight:700;letter-spacing:0.06em;color:#f7f4ee;"">
        EQUAMERIDIAN <span style=""color:#c9a227;"">HOLDINGS</span>
      </div>
      <div style=""font-size:11px;letter-spacing:0.12em;text-transform:uppercase;color:rgba(247,244,238,0.55);margin-top:4px;"">
        Machinery Marketplace
      </div>
    </td>
  </tr>
  <tr>
    <td style=""padding:28px 28px 8px;"">
      {innerBody}
    </td>
  </tr>
  <tr>
    <td style=""padding:8px 28px 24px;"">
      <p style=""margin:0;font-size:13px;line-height:1.5;color:#9a958c;"">
        You received this email because of activity on your EquaMeridian account.
        Log in any time to manage bookings, listings, and messages.
      </p>
    </td>
  </tr>
  <tr>
    <td style=""background:#0e0d0b;padding:14px 28px;border-top:2px solid #c9a227;"">
      <div style=""font-size:11px;color:rgba(247,244,238,0.55);line-height:1.5;"">
        © {year} EquaMeridian Holdings · Machinery Marketplace<br/>
        This message is confidential. If you received it in error, please delete it.
      </div>
    </td>
  </tr>
</table>
</td></tr>
</table>
</body>
</html>";
    }

    private async Task SendAsync(string to, string toName, string subject, string body)
    {
        var apiKey = _config["Email:SendGridApiKey"];
        var html = WrapHtml(subject, body);
        var plain = System.Text.RegularExpressions.Regex.Replace(body, "<.*?>", " ");
        plain = System.Text.RegularExpressions.Regex.Replace(plain, @"\s+", " ").Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine($"[EMAIL] (no API key) To: {to} | Subject: {subject}");
            return;
        }

        var client = new SendGridClient(apiKey);
        var msg = MailHelper.CreateSingleEmail(
            from: new EmailAddress(_fromEmail, _fromName),
            to: new EmailAddress(to, toName),
            subject: subject,
            plainTextContent: plain,
            htmlContent: html
        );

        var response = await client.SendEmailAsync(msg);

        if ((int)response.StatusCode >= 400)
        {
            var responseBody = await response.Body.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"SendGrid returned {response.StatusCode}: {responseBody}");
        }
    }

    private async Task SendWithRetryAsync(Func<Task> send, string auditType)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await send();
                return;
            }
            catch when (attempt < 3)
            {
                await Task.Delay(500 * attempt);
            }
            catch (Exception ex)
            {
                await _audit.LogAsync(null, "NOTIFICATION_DISPATCH_FAILED",
                    $"{auditType} failed after 3 attempts: {ex.Message}",
                    null, null, null, null);
            }
        }
    }
}