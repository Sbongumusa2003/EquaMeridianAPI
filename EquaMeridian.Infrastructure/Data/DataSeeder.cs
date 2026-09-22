using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EquaMeridian.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration? config = null)
    {
        await db.Database.MigrateAsync();

        var admins = new[]
        {
            new
            {
                FullName = "Super Admin",
                Email    = config?["Seed:AdminEmail"]    ?? "admin@equameridian.co.za",
                Password = config?["Seed:AdminPassword"] ?? GenerateFallbackPassword("admin")
            },
            new
            {
                FullName = "System Admin",
                Email    = config?["Seed:SysAdminEmail"]    ?? "sysadmin@equameridian.co.za",
                Password = config?["Seed:SysAdminPassword"] ?? GenerateFallbackPassword("sysadmin")
            }
        };

        foreach (var a in admins)
        {
            if (!await db.Users.AnyAsync(u => u.Email == a.Email))
            {
                db.Users.Add(new User
                {
                    FullName = a.FullName,
                    Email = a.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(a.Password),
                    Role = "admin",
                    AccountStatus = "Active",
                    CreatedDate = AppTime.Now
                });
            }
        }

        await db.SaveChangesAsync();

        var defaultCategories = new[]
        {
            "Excavators", "Cranes", "Loaders", "Graders", "Compactors", "Trucks"
        };

        foreach (var name in defaultCategories)
        {
            if (!await db.Categories.AnyAsync(c => c.Name == name))
            {
                db.Categories.Add(new Category { Name = name });
            }
        }

        await db.SaveChangesAsync();

        var defaultServiceAreas = new[]
        {
            "Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal", "Limpopo",
            "Mpumalanga", "North West", "Northern Cape", "Western Cape"
        };

        foreach (var name in defaultServiceAreas)
        {
            if (!await db.ServiceAreas.AnyAsync(s => s.Name == name))
            {
                db.ServiceAreas.Add(new ServiceArea { Name = name });
            }
        }

        await db.SaveChangesAsync();

        // Province -> City -> Suburb data tree (see LocationTreeController). Seeded with the 9 SA
        // provinces and a couple of cities/suburbs each so the tree has real depth to demonstrate,
        // while staying fully editable (add/update/delete at every level) from the admin frontend.
        if (!await db.Provinces.AnyAsync())
        {
            var locationTree = new Dictionary<string, Dictionary<string, string[]>>
            {
                ["Gauteng"] = new()
                {
                    ["Johannesburg"] = new[] { "Sandton", "Randburg", "Soweto" },
                    ["Pretoria"] = new[] { "Centurion", "Hatfield" }
                },
                ["Western Cape"] = new()
                {
                    ["Cape Town"] = new[] { "Sea Point", "Bellville", "Khayelitsha" },
                    ["Stellenbosch"] = new[] { "Die Boord" }
                },
                ["KwaZulu-Natal"] = new()
                {
                    ["Durban"] = new[] { "Umhlanga", "Berea" },
                    ["Pietermaritzburg"] = new[] { "Hayfields" }
                },
                ["Eastern Cape"] = new() { ["Gqeberha"] = new[] { "Summerstrand" } },
                ["Free State"] = new() { ["Bloemfontein"] = new[] { "Westdene" } },
                ["Limpopo"] = new() { ["Polokwane"] = new[] { "Bendor" } },
                ["Mpumalanga"] = new() { ["Nelspruit"] = new[] { "West Acres" } },
                ["North West"] = new() { ["Rustenburg"] = new[] { "Safarituine" } },
                ["Northern Cape"] = new() { ["Kimberley"] = new[] { "Hadison Park" } },
            };

            foreach (var (provinceName, cities) in locationTree)
            {
                var province = new Province { Name = provinceName };
                foreach (var (cityName, suburbs) in cities)
                {
                    var city = new City { Name = cityName };
                    foreach (var suburbName in suburbs)
                        city.Suburbs.Add(new Suburb { Name = suburbName });
                    province.Cities.Add(city);
                }
                db.Provinces.Add(province);
            }

            await db.SaveChangesAsync();
        }

        var defaultDocTypes = new[]
        {
            new { TypeName = "Business Registration Certificate", IsRequired = true,  AppliesToRole = (string?)"Supplier" },
            new { TypeName = "Tax Clearance Certificate",         IsRequired = true,  AppliesToRole = (string?)null },
            new { TypeName = "Proof of Address",                 IsRequired = true,  AppliesToRole = (string?)null },
            new { TypeName = "Identity Document",                IsRequired = true,  AppliesToRole = (string?)null },
            new { TypeName = "Equipment Safety Certificate",     IsRequired = false, AppliesToRole = (string?)"Supplier" }
        };

        foreach (var t in defaultDocTypes)
        {
            var existing = await db.DocumentTypes.FirstOrDefaultAsync(x => x.TypeName == t.TypeName);
            if (existing == null)
            {
                db.DocumentTypes.Add(new DocumentType
                {
                    TypeName = t.TypeName,
                    IsRequired = t.IsRequired,
                    AppliesToRole = t.AppliesToRole
                });
            }
        }

        await db.SaveChangesAsync();

        if (!await db.FeeConfigurations.AnyAsync())
        {
            db.FeeConfigurations.Add(new FeeConfiguration
            {
                CommissionRate = 8m,
                MinFee = 0m,
                MaxFee = 0m,
                VATInclusive = false,
                VATRate = 15m,
                DeliveryBaseFee = 350m,
                DeliveryFreeRadiusKm = 50m,
                DeliveryRatePerKm = 15m,
                UpdatedAt = AppTime.Now
            });
            await db.SaveChangesAsync();
        }

        if (!await db.DiscountTiers.AnyAsync())
        {
            db.DiscountTiers.AddRange(
                new DiscountTier { CategoryID = null, MinDays = 1, MaxDays = 6, DiscountPercent = 0m, UpdatedAt = AppTime.Now },
                new DiscountTier { CategoryID = null, MinDays = 7, MaxDays = 13, DiscountPercent = 5m, UpdatedAt = AppTime.Now },
                new DiscountTier { CategoryID = null, MinDays = 14, MaxDays = 29, DiscountPercent = 10m, UpdatedAt = AppTime.Now },
                new DiscountTier { CategoryID = null, MinDays = 30, MaxDays = null, DiscountPercent = 15m, UpdatedAt = AppTime.Now }
            );
            await db.SaveChangesAsync();
        }
        var permissionKeys = new[]
        {
            "Dashboard.View",
            "Reports.View", "Reports.Export",
            "Users.ManageInternal", "Roles.Manage",
            "Listings.Manage", "Inspections.Manage",
            "Disputes.Manage", "Refunds.Manage", "Payouts.Manage", "Invoices.View",
            "ContentRules.Manage", "Reviews.Manage",
            "Campaigns.Manage", "Announcements.Manage", "Notifications.Broadcast",
            "PlatformFees.Manage", "Timers.Manage", "DataExport.Manage",
            "Backup.Manage", "AuditLog.View", "Locations.Manage"
        };

        foreach (var key in permissionKeys)
        {
            if (!await db.Permissions.AnyAsync(p => p.PermissionKey == key))
            {
                db.Permissions.Add(new Permission { PermissionKey = key });
            }
        }
        await db.SaveChangesAsync();

        var allPermissions = await db.Permissions.ToListAsync();
        Permission Perm(string key) => allPermissions.First(p => p.PermissionKey == key);

        var systemRoles = new (string RoleName, string Description, string[] PermissionKeys)[]
        {
            ("admin", "Full platform administrator.", permissionKeys),
            ("Supplier", "Machinery supplier account.", Array.Empty<string>()),
            ("Contractor", "Machinery contractor/renter account.", Array.Empty<string>())
        };

        foreach (var (roleName, description, keys) in systemRoles)
        {
            var existing = await db.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.RoleName == roleName);

            if (existing == null)
            {
                db.Roles.Add(new Role
                {
                    RoleName = roleName,
                    Description = description,
                    IsSystemRole = true,
                    CreatedDate = AppTime.Now,
                    RolePermissions = keys.Select(k => new RolePermission { Permission = Perm(k) }).ToList()
                });
            }
        }
        await db.SaveChangesAsync();

        // The admin role should always have every permission — including ones added after it was
        // first seeded (e.g. an existing dev DB predating a newly-introduced key like Locations.Manage).
        var adminRole = await db.Roles.Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.RoleName == "admin");
        if (adminRole != null)
        {
            var alreadyGranted = adminRole.RolePermissions.Select(rp => rp.PermissionID).ToHashSet();
            foreach (var key in permissionKeys)
            {
                var perm = Perm(key);
                if (!alreadyGranted.Contains(perm.PermissionID))
                    adminRole.RolePermissions.Add(new RolePermission { RoleID = adminRole.RoleID, PermissionID = perm.PermissionID });
            }
            await db.SaveChangesAsync();
        }

        var defaultBlockedTerms = new[]
        {
            "fuck", "shit", "bitch", "asshole", "cunt" , "faggot"
        };

        foreach (var term in defaultBlockedTerms)
        {
            if (!await db.BlockedTerms.AnyAsync(t => t.Term == term))
            {
                db.BlockedTerms.Add(new BlockedTerm { Term = term });
            }
        }
        await db.SaveChangesAsync();
    }

    private static string GenerateFallbackPassword(string prefix)
    {
        var random = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        Console.WriteLine($"[SEED WARNING] No password configured for '{prefix}' admin. " +
                          "A random password was generated. Use forgot-password to set a real one.");
        return random;
    }
}