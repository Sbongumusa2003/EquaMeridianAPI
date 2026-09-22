using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EquaMeridian.Tests.Helpers;

public static class TestDbContextFactory
{
    public static AppDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
