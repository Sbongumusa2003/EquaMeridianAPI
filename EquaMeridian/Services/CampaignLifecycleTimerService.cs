using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Promotes Scheduled → Active when StartDate arrives, and Active → Ended when EndDate passes.
/// Mirrors QuoteExpiryTimerService so campaign status stays accurate without manual admin edits.
/// </summary>
public class CampaignLifecycleTimerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CampaignLifecycleTimerService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public CampaignLifecycleTimerService(
        IServiceScopeFactory scopeFactory,
        ILogger<CampaignLifecycleTimerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Campaign lifecycle timer started (interval {Interval}).", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Campaign lifecycle tick failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = AppTime.Now;

        var toActivate = await db.Campaigns
            .Where(c => c.Status == "Scheduled"
                        && c.DeletedAt == null
                        && c.StartDate <= now
                        && c.EndDate >= now)
            .ToListAsync(ct);

        foreach (var c in toActivate)
            c.Status = "Active";

        var toEnd = await db.Campaigns
            .Where(c => c.Status == "Active"
                        && c.DeletedAt == null
                        && c.EndDate < now)
            .ToListAsync(ct);

        foreach (var c in toEnd)
            c.Status = "Ended";

        if (toActivate.Count > 0 || toEnd.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Campaign lifecycle: activated {Activated}, ended {Ended}.",
                toActivate.Count, toEnd.Count);
        }
    }
}
