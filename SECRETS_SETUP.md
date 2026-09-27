# Setting up local secrets after this fix

The values below were removed from `appsettings.json` / `appsettings.Development.json`
so they no longer sit in source control. You need to supply them yourself, either
via .NET User Secrets (local dev) or environment variables (Render deployment).

## Keys that need a real value

- `ConnectionStrings:DefaultConnection`
- `Jwt:Key`
- `PayFast:MerchantId`
- `PayFast:MerchantKey`
- `PayFast:Passphrase`
- `Seed:AdminPassword`
- `Seed:SysAdminPassword`

## Option A — Local development (.NET User Secrets)

Run these once from the `EquaMeridian` project folder (where the .csproj lives):

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Port=5432;Database=equameridian;Username=equameridian_user;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
dotnet user-secrets set "Jwt:Key" "YOUR_JWT_SIGNING_KEY"
dotnet user-secrets set "PayFast:MerchantId" "YOUR_MERCHANT_ID"
dotnet user-secrets set "PayFast:MerchantKey" "YOUR_MERCHANT_KEY"
dotnet user-secrets set "PayFast:Passphrase" "YOUR_PASSPHRASE"
dotnet user-secrets set "Seed:AdminPassword" "YOUR_ADMIN_PASSWORD"
dotnet user-secrets set "Seed:SysAdminPassword" "YOUR_SYSADMIN_PASSWORD"
```

These are stored outside the project folder (in your user profile under
`%APPDATA%\Microsoft\UserSecrets` on Windows or `~/.microsoft/usersecrets` on
Linux/Mac) and are automatically loaded when `ASPNETCORE_ENVIRONMENT=Development`.
They are never committed to git.

## Option B — Render (or any hosting) deployment

Set these as environment variables in the Render dashboard for the API service.
.NET configuration maps `:` in a key to `__` (double underscore) in an
environment variable name:

| Config key                              | Environment variable name         |
|------------------------------------------|-----------------------------------|
| `ConnectionStrings:DefaultConnection`   | `ConnectionStrings__DefaultConnection` |
| `Jwt:Key`                                | `Jwt__Key`                        |
| `PayFast:MerchantId`                     | `PayFast__MerchantId`             |
| `PayFast:MerchantKey`                    | `PayFast__MerchantKey`            |
| `PayFast:Passphrase`                     | `PayFast__Passphrase`             |
| `Seed:AdminPassword`                     | `Seed__AdminPassword`             |
| `Seed:SysAdminPassword`                  | `Seed__SysAdminPassword`          |

No code changes are needed — the built-in `IConfiguration` provider chain reads
environment variables automatically and they override anything in `appsettings.json`.

## Note

The values that were in these files before this fix (DB password, JWT key,
PayFast merchant key/passphrase, seed admin passwords) were left unrotated at
your request. If this repository is or ever becomes public, those old values
should be treated as compromised.
