using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EquaMeridian.Infrastructure.Data;

public static class DatabaseObjectsInitializer
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        // Temporarily disabled for PostgreSQL deployment on Render.
        // The original file contained SQL Server specific T-SQL.
        await Task.CompletedTask;
    }
}