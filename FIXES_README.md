# EquaMeridian API – Fixed build (compile fix applied)

Includes restricted session, SaAddress, validation middleware, booking-summary, and repository wiring.

## Build fix (27 Sep 2026)
- Added missing `ResolveAddress` helper in `QuotationRepository.cs` (CS0103).

```bash
dotnet ef database update --project EquaMeridian.Infrastructure --startup-project EquaMeridian
dotnet build
```
