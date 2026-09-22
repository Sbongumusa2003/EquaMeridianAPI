using EquaMeridian.Infrastructure.Data;
using EquaMeridian.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EquaMeridian.Tests.Services;

public class ContentModerationServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ContentModerationService _sut;

    public ContentModerationServiceTests()
    {
        ResetStaticCache();
        _db = TestDbContextFactory.Create();
        _db.BlockedTerms.AddRange(
            new BlockedTerm { BlockedTermID = 1, Term = "spam", CreatedDate = AppTime.Now },
            new BlockedTerm { BlockedTermID = 2, Term = "scam", CreatedDate = AppTime.Now }
        );
        _db.SaveChanges();
        _sut = new ContentModerationService(_db);
    }

    public void Dispose()
    {
        ResetStaticCache();
        _db.Dispose();
    }

    private static void ResetStaticCache()
    {
        var type = typeof(ContentModerationService);
        type.GetField("_cachedTerms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.SetValue(null, null);
        type.GetField("_cacheExpiresAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.SetValue(null, DateTime.MinValue);
    }

    [Fact]
    public async Task IsCleanAsync_CleanText_ReturnsTrue()
    {
        var clean = await _sut.IsCleanAsync("Great excavator", "Worked perfectly on site.");
        clean.Should().BeTrue();
    }

    [Fact]
    public async Task IsCleanAsync_ContainsBlockedTerm_ReturnsFalse()
    {
        var dirty = await _sut.IsCleanAsync("Buy now", "This is a total scam listing.");
        dirty.Should().BeFalse();
    }

    [Fact]
    public async Task IsCleanAsync_IsCaseInsensitive()
    {
        var dirty = await _sut.IsCleanAsync("SPAM alert", "Normal review body.");
        dirty.Should().BeFalse();
    }

    [Fact]
    public async Task GetBlockedTermsAsync_ReturnsSortedTerms()
    {
        var terms = await _sut.GetBlockedTermsAsync();
        terms.Should().BeInAscendingOrder();
        terms.Should().Contain(new[] { "scam", "spam" });
    }

    [Fact]
    public async Task AddBlockedTermAsync_Empty_Fails()
    {
        var (success, error) = await _sut.AddBlockedTermAsync("   ", adminId: 1);
        success.Should().BeFalse();
        error.Should().Contain("empty");
    }

    [Fact]
    public async Task AddBlockedTermAsync_Duplicate_Fails()
    {
        var (success, error) = await _sut.AddBlockedTermAsync("Spam", adminId: 1);
        success.Should().BeFalse();
        error.Should().Contain("already blocked");
    }

    [Fact]
    public async Task AddBlockedTermAsync_NewTerm_Succeeds()
    {
        var (success, error) = await _sut.AddBlockedTermAsync("  Fraud  ", adminId: 42);
        success.Should().BeTrue();
        error.Should().BeNull();

        var terms = await _sut.GetBlockedTermsAsync();
        terms.Should().Contain("fraud");
    }

    [Fact]
    public async Task RemoveBlockedTermAsync_NotFound_Fails()
    {
        var (success, error) = await _sut.RemoveBlockedTermAsync(9999);
        success.Should().BeFalse();
        error.Should().Contain("not found");
    }

    [Fact]
    public async Task RemoveBlockedTermAsync_Existing_Succeeds()
    {
        var (success, error) = await _sut.RemoveBlockedTermAsync(1);
        success.Should().BeTrue();
        error.Should().BeNull();

        var terms = await _sut.GetBlockedTermsAsync();
        terms.Should().NotContain("spam");
    }
}
