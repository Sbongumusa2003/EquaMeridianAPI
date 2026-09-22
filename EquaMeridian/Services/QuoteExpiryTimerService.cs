using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class QuoteExpiryTimerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly TimeSpan FallbackInterval = TimeSpan.FromMinutes(60);

    public QuoteExpiryTimerService(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = FallbackInterval;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
                var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var sms = scope.ServiceProvider.GetRequiredService<ISmsService>();

                var config = await db.TimerConfigurations.FirstOrDefaultAsync(
                    t => t.TimerKey == "QuoteExpiryCheck", stoppingToken);
                if (config == null)
                {
                    config = new TimerConfiguration { TimerKey = "QuoteExpiryCheck" };
                    db.TimerConfigurations.Add(config);
                    await db.SaveChangesAsync(stoppingToken);
                }

                interval = TimeSpan.FromMinutes(Math.Max(1, config.IntervalMinutes));

                if (config.IsEnabled)
                {
                    var now = AppTime.Now;
                    var expired = await db.Quotations
                        .Include(q => q.Contractor)
                        .Include(q => q.Supplier)
                        .Include(q => q.Listing)
                        .Where(q => q.Status == "Requested" || q.Status == "Submitted")
                        .Where(q => q.QuoteValidUntil != null
                            ? q.QuoteValidUntil <= now
                            : q.RequestedDate.AddHours(config.QuoteExpiryHours) <= now)
                        .ToListAsync(stoppingToken);

                    foreach (var quotation in expired)
                    {
                        quotation.Status = "Expired";

                        await notifications.CreateAsync(quotation.ContractorID, "QuoteExpired",
                            "Your quote has expired",
                            $"Quotation #{quotation.QuotationID} for \"{quotation.Listing.ListingTitle}\" expired.",
                            "Quotation", quotation.QuotationID, emailUser: false);
                        // Supplier has no dedicated expiry email — notification email covers them
                        await notifications.CreateAsync(quotation.SupplierID, "QuoteExpired",
                            "A quotation you were handling has expired",
                            $"Quotation #{quotation.QuotationID} for \"{quotation.Listing.ListingTitle}\" expired unanswered.",
                            "Quotation", quotation.QuotationID);

                        await email.SendQuotationExpiredEmailAsync(
                            quotation.Contractor.Email, quotation.Contractor.FullName,
                            quotation.QuotationID, quotation.Listing.ListingTitle);
                        await sms.SendQuotationExpiredSmsAsync(
                            quotation.Contractor.PhoneNumber, quotation.Contractor.FullName,
                            quotation.QuotationID, quotation.Listing.ListingTitle);
                    }

                    config.LastRunAt = now;
                    config.LastRunExpiredCount = expired.Count;
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuoteExpiryTimerService] Cycle failed: {ex.Message}");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
