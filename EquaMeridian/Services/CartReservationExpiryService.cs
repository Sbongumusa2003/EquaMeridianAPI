using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class CartReservationExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    public CartReservationExpiryService(IServiceScopeFactory scopes) => _scopes = scopes;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = AppTime.Now;
                var stale = await db.CartItems.Include(c => c.Listing)
                    .Where(c => c.ReservedUntil < now)
                    .ToListAsync(stoppingToken);
                foreach (var item in stale)
                {
                    if (item.Listing != null)
                    {
                        item.Listing.UnitsReserved = Math.Max(0, item.Listing.UnitsReserved - item.Quantity);
                        item.Listing.UnitsAvailable += item.Quantity;
                    }
                    db.CartItems.Remove(item);
                }
                if (stale.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[CartReservationExpiry] " + ex.Message);
            }
            try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
