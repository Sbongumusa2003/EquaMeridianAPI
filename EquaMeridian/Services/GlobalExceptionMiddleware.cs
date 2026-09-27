using System.Net;
using System.Text.Json;
using EquaMeridian.Validation;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (statusCode, friendlyMessage) = Map(ex);

            _logger.LogError(ex, "Unhandled exception on {Method} {Path} -> {StatusCode}",
                context.Request.Method, context.Request.Path, (int)statusCode);

            if (statusCode == HttpStatusCode.InternalServerError)
            {
                try
                {
                    var audit = context.RequestServices.GetService<IAuditService>();
                    if (audit != null)
                    {
                        var userIdClaim = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        int? userId = int.TryParse(userIdClaim, out var id) ? id : null;
                        var ip = context.Connection.RemoteIpAddress?.ToString();

                        await audit.LogAsync(userId, "UNHANDLED_EXCEPTION",
                            $"{context.Request.Method} {context.Request.Path}: {ex.GetType().Name}: {ex.Message}",
                            null, null, null, ip);
                    }
                }
                catch
                {
                    // Never let audit logging itself take down the error response.
                }
            }

            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var isDev = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
            var body = new
            {
                message = friendlyMessage,
                statusCode = (int)statusCode,
                traceId = context.TraceIdentifier,
                detail = isDev ? ex.ToString() : null
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }

    /// <summary>
    /// Maps any unhandled exception to an HTTP status and a message that is safe and useful for the user.
    /// Technical detail (SQL text, stack traces, framework messages) never leaves the server in production.
    /// </summary>
    public static (HttpStatusCode StatusCode, string Message) Map(Exception ex) => ex switch
    {
        KeyNotFoundException => (HttpStatusCode.NotFound, "The requested resource was not found."),
        UnauthorizedAccessException => (HttpStatusCode.Forbidden, "You don't have permission to do that."),

        // Malformed request bodies (bad JSON, body too large, wrong content type).
        Microsoft.AspNetCore.Http.BadHttpRequestException bad => bad.StatusCode switch
        {
            StatusCodes.Status413PayloadTooLarge => (HttpStatusCode.RequestEntityTooLarge, "The file or request is too large."),
            StatusCodes.Status415UnsupportedMediaType => (HttpStatusCode.UnsupportedMediaType, "That file or data type isn't supported."),
            _ => (HttpStatusCode.BadRequest, "The request wasn't in the format we expected. Please check the information and try again.")
        },
        JsonException => (HttpStatusCode.BadRequest, "The information sent wasn't in the format we expected. Please check it and try again."),
        ArgumentException or FormatException => (HttpStatusCode.BadRequest, "The request contained invalid data."),

        // Business-rule messages we wrote ourselves are shown as-is; framework messages are replaced.
        InvalidOperationException when ex is Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException =>
            (HttpStatusCode.ServiceUnavailable, "The service is busy right now. Please try again in a moment."),
        InvalidOperationException =>
            (HttpStatusCode.Conflict, UserFacingErrors.MessageOrFallback(ex, "That action can't be completed right now. Please refresh and try again.")),

        // Database problems.
        Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException =>
            (HttpStatusCode.Conflict, "This record was changed by someone else. Please refresh and try again."),
        Microsoft.EntityFrameworkCore.DbUpdateException db => MapDatabase(db),
        Microsoft.Data.SqlClient.SqlException =>
            (HttpStatusCode.ServiceUnavailable, "We couldn't reach the database. Please try again in a moment."),

        // A partner service (PayFast, e-mail, SMS, shipping) is down or slow.
        HttpRequestException =>
            (HttpStatusCode.BadGateway, "A connected service is unavailable right now. Please try again shortly."),
        TimeoutException or TaskCanceledException or OperationCanceledException =>
            (HttpStatusCode.RequestTimeout, "The request took too long and was cancelled. Please try again."),

        _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again.")
    };

    private static (HttpStatusCode StatusCode, string Message) MapDatabase(Microsoft.EntityFrameworkCore.DbUpdateException ex)
    {
        var number = (ex.InnerException as Microsoft.Data.SqlClient.SqlException)?.Number;
        return number switch
        {
            2601 or 2627 => (HttpStatusCode.Conflict, "That already exists. Please use a different value."),
            547 => (HttpStatusCode.Conflict, "This record is linked to other records, so that change can't be made."),
            2628 or 8152 => (HttpStatusCode.BadRequest, "One of the values you entered is too long. Please shorten it and try again."),
            515 => (HttpStatusCode.BadRequest, "A required value is missing. Please complete all required fields."),
            1205 => (HttpStatusCode.Conflict, "The system was busy. Please try again."),
            _ => (HttpStatusCode.Conflict, "The change couldn't be saved because it conflicts with existing data.")
        };
    }
}
