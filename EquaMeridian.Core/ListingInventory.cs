/// <summary>
/// Single source of truth for listing unit counters.
///
/// Semantics:
///   UnitsAvailable  – free stock that can be cart-held or booked
///   UnitsReserved   – soft holds only (active cart lines). Never used for confirmed bookings.
///   UnitsUnderMaintenance – damaged / offline units
///
/// Lifecycle:
///   SoftHold          : Available −, Reserved +
///   SoftRelease       : Available +, Reserved −
///   ConfirmFromHold   : Reserved −          (Available already reduced by SoftHold)
///   ConfirmFromStock  : Available −          (quote accept, no prior cart hold)
///   ReleaseFromHire   : Available +          (cancel or successful return)
///   MoveToMaintenance : UnderMaintenance +   (failed return inspection)
/// </summary>
public static class ListingInventory
{
    public static void SoftHold(Listing listing, int qty)
    {
        if (listing == null || qty <= 0) return;
        listing.UnitsAvailable = Math.Max(0, listing.UnitsAvailable - qty);
        listing.UnitsReserved += qty;
    }

    public static void SoftRelease(Listing listing, int qty)
    {
        if (listing == null || qty <= 0) return;
        listing.UnitsReserved = Math.Max(0, listing.UnitsReserved - qty);
        listing.UnitsAvailable += qty;
    }

    /// <summary>
    /// Convert a cart soft-hold into a confirmed booking.
    /// Units stay out of Available; only the Reserved counter is cleared.
    /// </summary>
    public static void ConfirmFromHold(Listing listing, int qty)
    {
        if (listing == null || qty <= 0) return;
        if (listing.UnitsReserved >= qty)
        {
            listing.UnitsReserved -= qty;
            return;
        }

        // Fallback: hold was lost (e.g. partial expiry race) — claim from free stock.
        var shortfall = qty - listing.UnitsReserved;
        listing.UnitsReserved = 0;
        if (listing.UnitsAvailable < shortfall)
            throw new InvalidOperationException(
                $"Only {listing.UnitsAvailable} unit(s) of '{listing.ListingTitle}' remain.");
        listing.UnitsAvailable -= shortfall;
    }

    /// <summary>
    /// Confirm a booking with no prior cart hold (e.g. accepted quotation).
    /// Does NOT touch UnitsReserved.
    /// </summary>
    public static void ConfirmFromStock(Listing listing, int qty)
    {
        if (listing == null || qty <= 0) return;
        if (listing.UnitsAvailable < qty)
            throw new InvalidOperationException(
                $"Only {listing.UnitsAvailable} unit(s) of '{listing.ListingTitle}' remain.");
        listing.UnitsAvailable -= qty;
    }

    /// <summary>
    /// Return units from a finished/cancelled hire back to free stock.
    /// Does NOT touch UnitsReserved (confirmed bookings never held a reservation).
    /// </summary>
    public static void ReleaseFromHire(Listing listing, int qty)
    {
        if (listing == null || qty <= 0) return;
        listing.UnitsAvailable += qty;
        if (listing.AvailabilityStatus != "Suspended" && listing.AvailabilityStatus != "Pending Review")
            listing.AvailabilityStatus = "Active";
    }

    /// <summary>
    /// Move units from a failed return inspection into maintenance (not available).
    /// </summary>
    public static void MoveToMaintenance(Listing listing, int qty)
    {
        if (listing == null || qty <= 0) return;
        listing.UnitsUnderMaintenance += qty;
    }

    /// <summary>
    /// Complete a hire: restore to available on pass, or park in maintenance on fail.
    /// </summary>
    public static void CompleteHire(Listing listing, int qty, bool passedInspection)
    {
        if (passedInspection)
            ReleaseFromHire(listing, qty);
        else
            MoveToMaintenance(listing, qty);
    }
}
