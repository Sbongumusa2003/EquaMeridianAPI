# EquaMeridian API – Fixed build

This package is your deployed API with the following fixes applied:

## Included fixes
1. **Error / validation handling** – ModelStateMessages, improved GlobalExceptionMiddleware, Validation helpers
2. **Restricted session** – Disabled Supplier/Contractor can log in only to upload verification docs / request reactivation
3. **Structured SA address** – SaAddress / SaAddressDto on Listings, Bookings, CartItems, Quotations, ReturnRequests
4. **Admin booking summary** – GET admin/users/{id}/booking-summary
5. **Service completeness** – AuthService, InvoicePdf, PaymentGateway, PaymentSync updates

## After extract
```bash
dotnet ef database update --project EquaMeridian.Infrastructure --startup-project EquaMeridian
dotnet build
# deploy as usual
```

Migration: `EquaMeridian.Infrastructure/Migrations/20260918130000_AddStructuredSaAddressColumns.cs`
