using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextPool<AppDbContext>(opt =>
    opt.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql
            .EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null)
            .CommandTimeout(30)));
builder.Services.AddResponseCompression(opt =>
{
    opt.EnableForHttps = true;
    opt.Providers.Add<BrotliCompressionProvider>();
    opt.Providers.Add<GzipCompressionProvider>();
    opt.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/json" });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opt => opt.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(opt => opt.Level = System.IO.Compression.CompressionLevel.Fastest);

builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IListingImageRepository, ListingImageRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IListingRepository, ListingRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IFeeConfigurationRepository, FeeConfigurationRepository>();
builder.Services.AddScoped<IDisputeRepository, DisputeRepository>();
builder.Services.AddScoped<IRefundRepository, RefundRepository>();
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
builder.Services.AddScoped<IQuotationRepository, QuotationRepository>();
builder.Services.AddScoped<IInspectionRepository, InspectionRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<ILeaseAgreementRepository, LeaseAgreementRepository>();
builder.Services.AddScoped<IJobDocumentRepository, JobDocumentRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IContentModerationService, ContentModerationService>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IReportingRepository, ReportingRepository>();
builder.Services.AddScoped<ISupplierReportingRepository, SupplierReportingRepository>();
builder.Services.AddScoped<ITimerConfigurationRepository, TimerConfigurationRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPayoutRepository, PayoutRepository>();
builder.Services.AddScoped<IPricingEngine, PricingEngine>();
builder.Services.AddScoped<IDiscountTierRepository, DiscountTierRepository>();
builder.Services.AddScoped<IDeliveryDistanceService, PlaceholderDeliveryDistanceService>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<IWishlistRepository, WishlistRepository>();
builder.Services.AddSingleton<ChatbotIntentModel>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IPaymentGatewayService, PaymentGatewayService>();
builder.Services.AddScoped<IPaymentSyncService, PaymentSyncService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ISmsService, ClickSendSmsService>();
builder.Services.AddHostedService<QuoteExpiryTimerService>();
builder.Services.AddHostedService<CampaignLifecycleTimerService>();
builder.Services.AddHostedService<CartReservationExpiryService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("App:AllowedOrigins")
            .Get<string[]>();

        if (allowedOrigins?.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key is not configured. Set it via user-secrets or an environment variable.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name
        };
        // Critical for public browse: a bad/expired Bearer token must NOT turn
        // [AllowAnonymous] endpoints into 401. Treat failed auth as "no user".
        opt.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                context.NoResult();
                return System.Threading.Tasks.Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                // If auth failed but endpoint allows anonymous, skip the 401 challenge.
                if (context.AuthenticateFailure != null && context.HttpContext.GetEndpoint()?.Metadata
                        .GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null)
                {
                    context.HandleResponse();
                }
                return System.Threading.Tasks.Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(opt =>
{
    // Internal staff: built-in admin OR any custom role assigned under Admin > Roles.
    // Supplier / Contractor stay on their own portals and must not use admin APIs.
    opt.AddPolicy("AdminOnly", p => p.RequireAssertion(ctx =>
    {
        var role = ctx.User.Claims
            .FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role")
            ?.Value;
        if (string.IsNullOrWhiteSpace(role)) return false;
        return !string.Equals(role, "Supplier", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(role, "Contractor", StringComparison.OrdinalIgnoreCase);
    }));
    opt.AddPolicy("SupplierOnly", p => p.RequireAssertion(ctx =>
        ctx.User.Claims.Any(c =>
            (c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role") &&
            string.Equals(c.Value, "Supplier", StringComparison.OrdinalIgnoreCase))));
    opt.AddPolicy("ContractorOnly", p => p.RequireAssertion(ctx =>
        ctx.User.Claims.Any(c =>
            (c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role") &&
            string.Equals(c.Value, "Contractor", StringComparison.OrdinalIgnoreCase))));
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Always return { message: "..." } so the Angular register form can show a clear banner
        // (default ValidationProblemDetails is not read as err.error.message).
        options.InvalidModelStateResponseFactory = context =>
        {
            var msgs = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                    ? e.Exception?.Message
                    : e.ErrorMessage)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct()
                .ToList();

            var message = msgs.Count > 0
                ? string.Join(" ", msgs!)
                : "One or more validation errors occurred.";

            return new BadRequestObjectResult(new { message });
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EquaMeridian API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token (without 'Bearer ' prefix)"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {{
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id   = "Bearer"
            }
        },
        Array.Empty<string>()
    }});
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DataSeeder.SeedAsync(db, builder.Configuration);
    // DatabaseObjectsInitializer contains SQL Server-specific T-SQL.
    // Disabled for PostgreSQL deployment on Render.
    // await DatabaseObjectsInitializer.InitializeAsync(db);
}

app.UseMiddleware<GlobalExceptionMiddleware>();
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// Enable Swagger in all environments during initial deployment (you can restrict later)
app.UseSwagger();
app.UseSwaggerUI();

app.UseResponseCompression();

app.UseStaticFiles();
var uploadsPhysicalPath = Path.Combine(
    builder.Environment.ContentRootPath, "uploads");

Directory.CreateDirectory(uploadsPhysicalPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        uploadsPhysicalPath),
    RequestPath = "/uploads"
});

app.UseRouting();
app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Lightweight, unauthenticated endpoint for uptime monitors (Checkly, etc.).
// Deliberately does not touch the database - it just proves the process is alive
// and responsive, so checks stay fast and don't count against DB connection limits.
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.Run();