/// <summary>
/// Centralised listing unit counters.
///
/// Lifecycle (current product rules):
///   Cart / quote accept     : NO inventory change (units stay available to others)
///   Dual lease signature    : ConfirmFromStock — claim UnitsAvailable
///   Cancel before dual sign : no inventory change
///   Cancel after dual sign / non-payment / complete hire : ReleaseFromHire
///   Failed return inspection: MoveToMaintenance
///
/// SoftHold / SoftRelease / ConfirmFromHold are LEGACY (cart no longer reserves stock).
/// Kept as no-op-safe helpers only if an old code path still references them; do not use for new features.
/// UnitsReserved is legacy and must not be incremented by cart flows.
/// </summary>
public static class ListingInventory
{
    [Obsolete("Cart no longer reserves stock. Do not call SoftHold for new features.")]
    public static void SoftHold(Listing listing, int qty)
    {
        // Intentionally no-op: product rule is claim-on-dual-sign only.
        // Left callable so any stray call does not under-count UnitsAvailable.
    }

    [Obsolete("Cart no longer reserves stock. Do not call SoftRelease for new features.")]
    public static void SoftRelease(Listing listing, int qty)
    {
        // Intentionally no-op (pairs with SoftHold no-op).
    }

    [Obsolete("Cart no longer holds stock. Use ConfirmFromStock at dual lease signature.")]
    public static void ConfirmFromHold(Listing listing, int qty)
    {
        ConfirmFromStock(listing, qty);
    }

    /// <summary>
    /// Claim free stock when both parties have signed the lease (or equivalent hard commit).
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
    /// Return units from a finished/cancelled hire (after stock was claimed) back to free stock.
    /// </summary>
    public static void ReleaseFromHire(Listing listing, int qty)
    {
        if (listing == null || qty <= 0) return;
        listing.UnitsAvailable += qty;
        if (listing.AvailabilityStatus != "Suspended" && listing.AvailabilityStatus != "Pending Review")
            listing.AvailabilityStatus = "Active";
    }

    public static void MoveToMaintenance(Listing listing, int qty)
    {
        if (listing == null || qty <= 0) return;
        listing.UnitsUnderMaintenance += qty;
        if (listing.AvailabilityStatus != "Suspended" && listing.AvailabilityStatus != "Pending Review")
            listing.AvailabilityStatus = "Active";
    }

    /// <summary>
    /// After return inspection: release to free stock if passed, else move to maintenance.
    /// </summary>
    public static void CompleteHire(Listing listing, int qty, bool passedInspection)
    {
        if (listing == null || qty <= 0) return;
        if (passedInspection)
            ReleaseFromHire(listing, qty);
        else
            MoveToMaintenance(listing, qty);
    }

    /// <summary>
    /// Soft availability for UI: free stock minus quantity still on bookings awaiting dual signature.
    /// Does not hard-block cart add — advisory only.
    /// </summary>
    public static int EffectiveAvailable(int unitsAvailable, int pendingAwaitingSignatureQty)
        => Math.Max(0, unitsAvailable - Math.Max(0, pendingAwaitingSignatureQty));
}
