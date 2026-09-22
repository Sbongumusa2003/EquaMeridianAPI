public interface IEmailService
{
    Task SendLockoutEmailAsync(string email, string name);
    Task SendPasswordResetEmailAsync(string email, string name, string resetUrl);
    Task SendPasswordChangedNotificationAsync(string email, string name);
    Task SendReadyForPickupEmailAsync(string email, string name, int bookingId, string listingTitle);
    Task SendReadyForReturnPickupEmailAsync(string email, string name, int bookingId, string listingTitle);
    Task SendAccountStatusChangedAsync(string email, string name, string newStatus);
    Task SendListingStatusChangedAsync(string email, string name,
                                       int listingId, string newStatus, string? reason);
    Task SendNewListingPendingReviewAsync(string adminEmail, int listingId, string supplierName);
    Task SendNewSupplierPendingReviewAsync(string adminEmail, int userId, string supplierName, string companyName);
    Task SendDisputeResolutionEmailAsync(string email, string name, int disputeId,
                                         string resolutionOutcome, string resolutionNotes);
    Task SendQuotationSubmittedEmailAsync(string email, string name, int quotationId, string listingTitle);
    Task SendDocumentReviewedEmailAsync(string email, string name, int docId, string docName, string decision, string? reason = null);
    Task SendInspectionRequestedEmailAsync(string email, string name, int inspectionId,
                                           string listingTitle, DateTime scheduledDate);
    Task SendInspectionRequestedAdminEmailAsync(string adminEmail, int inspectionId,
                                                string listingTitle, DateTime scheduledDate,
                                                string requesterName);
    Task SendInspectionOutcomeConfirmedEmailAsync(string email, string name, int inspectionId,
                                                  string listingTitle, string outcome);
    Task SendDeliveryConfirmedEmailAsync(string email, string name, int bookingId, string listingTitle);
    Task SendQuotationRequestedEmailAsync(string email, string name, int quotationId, string listingTitle);
    Task SendQuotationAcceptedEmailAsync(string email, string name, int quotationId, int bookingId, string listingTitle);
    Task SendQuotationRejectedEmailAsync(string email, string name, int quotationId, string listingTitle);
    Task SendInvoiceGeneratedEmailAsync(string email, string name, int invoiceId, string invoiceNumber, decimal amount);
    Task SendLeaseAgreementSignatureRequiredEmailAsync(string email, string name, int leaseAgreementId, string agreementNumber);
    Task SendLeaseAgreementFullyExecutedEmailAsync(string email, string name, int leaseAgreementId, string agreementNumber);
    Task SendReturnRequestedEmailAsync(string email, string name, int bookingId, string machinery,
                                       DateTime preferredPickupDate, string timeWindow, string pickupLocation, string reason);
    Task SendReturnConfirmedEmailAsync(string email, string name, int bookingId, string machinery, string condition);
    Task SendDisputeRaisedEmailAsync(string email, string name, int disputeId, int bookingId, string category);
    Task SendReviewSubmittedEmailAsync(string email, string name, int bookingId, string machinery, int overallRating, string reviewTitle);
    Task SendReviewUpdatedEmailAsync(string email, string name, int reviewId, string machinery, int overallRating);
    Task SendReviewDeletedEmailAsync(string email, string name, int reviewId, string machinery);
    Task SendNewMessageEmailAsync(string email, string name, string senderName, int threadId);
    Task SendInternalAccountCreatedEmailAsync(string email, string name, string role, string temporaryPassword);
    Task SendQuotationExpiredEmailAsync(string email, string name, int quotationId, string listingTitle);
    Task SendOtpCodeEmailAsync(string email, string name, string code);
    Task SendPaymentReceivedEmailAsync(string email, string name, int bookingId, string invoiceNumber, decimal supplierPayableAmount);
    Task SendBookingCancelledEmailAsync(string email, string name, int bookingId, string listingTitle, string cancelledByRole, string? reason);
    Task SendNotificationEmailAsync(string email, string name, string title, string body, string? type = null);
}
