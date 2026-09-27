# EquaMeridian API – Fixed build

## Fixes included
- Restricted session (Disabled Supplier/Contractor)
- SaAddress structured address model + repository wiring
- ModelStateMessages + GlobalExceptionMiddleware
- Admin GetBookingSummary
- QuotationRepository.ResolveAddress helper (compile fix)
- PaymentGatewayService restored to match IPaymentGatewayService (compile fix)

```bash
dotnet ef database update --project EquaMeridian.Infrastructure --startup-project EquaMeridian
dotnet build
```
