using EquaMeridian.DTOs.Auth;
using EquaMeridian.Infrastructure.Data;
using EquaMeridian.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace EquaMeridian.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<ISmsService> _sms = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IDocumentRepository> _documents = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<IWebHostEnvironment> _env = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _db = TestDbContextFactory.Create();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-signing-key-must-be-at-least-32-chars!",
                ["Jwt:Issuer"] = "EquaMeridian.Tests",
                ["Jwt:Audience"] = "EquaMeridian.Tests",
                ["Jwt:ExpiryMinutes"] = "60",
                ["Jwt:LongExpiryDays"] = "7"
            })
            .Build();

        _env.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());

        _sut = new AuthService(
            _db,
            config,
            _email.Object,
            _sms.Object,
            _audit.Object,
            _documents.Object,
            _env.Object,
            _notifications.Object);
    }

    public void Dispose() => _db.Dispose();

    private User SeedActiveUser(string email = "user@test.com", string password = "Password1!", string role = "Contractor")
    {
        var user = new User
        {
            UserID = 1,
            FullName = "Test User",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
            AccountStatus = "Active",
            TwoFactorEnabled = false,
            CreatedDate = AppTime.Now
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task RegisterAsync_NewContractor_Succeeds()
    {
        var dto = new RegisterRequest
        {
            FullName = "Jane Contractor",
            Email = "jane@example.com",
            Password = "SecurePass1",
            Role = "Contractor",
            PhoneNumber = "+27821234567"
        };

        var (success, message) = await _sut.RegisterAsync(dto, "127.0.0.1");

        success.Should().BeTrue();
        message.Should().NotBeNullOrEmpty();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == "jane@example.com");
        user.Should().NotBeNull();
        user!.Role.Should().Be("Contractor");
        BCrypt.Net.BCrypt.Verify("SecurePass1", user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_Fails()
    {
        SeedActiveUser("taken@example.com");

        var dto = new RegisterRequest
        {
            FullName = "Another",
            Email = "taken@example.com",
            Password = "SecurePass1",
            Role = "Contractor"
        };

        var (success, message) = await _sut.RegisterAsync(dto, "127.0.0.1");
        success.Should().BeFalse();
        message.Should().ContainEquivalentOf("already");
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        SeedActiveUser();

        var outcome = await _sut.LoginAsync(new LoginRequest
        {
            Email = "user@test.com",
            Password = "Password1!"
        }, "127.0.0.1");

        outcome.Should().NotBeNull();
        outcome!.RequiresOtp.Should().BeFalse();
        outcome.Login.Should().NotBeNull();
        outcome.Login!.Token.Should().NotBeNullOrEmpty();
        outcome.Login.Role.Should().Be("Contractor");
        outcome.Login.UserID.Should().Be(1);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull_AndIncrementsFailures()
    {
        var user = SeedActiveUser();

        var outcome = await _sut.LoginAsync(new LoginRequest
        {
            Email = "user@test.com",
            Password = "WrongPassword"
        }, "127.0.0.1");

        outcome.Should().BeNull();

        await _db.Entry(user).ReloadAsync();
        user.FailedAttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsNull()
    {
        var outcome = await _sut.LoginAsync(new LoginRequest
        {
            Email = "nobody@example.com",
            Password = "Password1!"
        }, "127.0.0.1");

        outcome.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_FiveFailures_LocksAccount()
    {
        var user = SeedActiveUser();

        for (var i = 0; i < 5; i++)
        {
            await _sut.LoginAsync(new LoginRequest
            {
                Email = "user@test.com",
                Password = "bad"
            }, "127.0.0.1");
        }

        await _db.Entry(user).ReloadAsync();
        user.AccountStatus.Should().Be("Locked");
        user.LockoutExpiry.Should().NotBeNull();
        _email.Verify(e => e.SendLockoutEmailAsync(user.Email, user.FullName), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_TwoFactorEnabled_RequiresOtp()
    {
        var user = SeedActiveUser();
        user.TwoFactorEnabled = true;
        user.PhoneNumber = "+27820001111";
        await _db.SaveChangesAsync();

        var outcome = await _sut.LoginAsync(new LoginRequest
        {
            Email = "user@test.com",
            Password = "Password1!"
        }, "127.0.0.1");

        outcome.Should().NotBeNull();
        outcome!.RequiresOtp.Should().BeTrue();
        outcome.OtpReference.Should().NotBeNullOrEmpty();
        outcome.Login.Should().BeNull();

        _email.Verify(e => e.SendOtpCodeEmailAsync(
            user.Email, user.FullName, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_PendingAccount_Throws()
    {
        var user = SeedActiveUser();
        user.AccountStatus = "Pending";
        await _db.SaveChangesAsync();

        var act = () => _sut.LoginAsync(new LoginRequest
        {
            Email = "user@test.com",
            Password = "Password1!"
        }, "127.0.0.1");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Pending*");
    }
}
