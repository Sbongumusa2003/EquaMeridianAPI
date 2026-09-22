using System.Net;
using System.Text.Json;

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
        catch (Exception ex)
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

    private static (HttpStatusCode StatusCode, string Message) Map(Exception ex) => ex switch
    {
        KeyNotFoundException => (HttpStatusCode.NotFound, "The requested resource was not found."),
        UnauthorizedAccessException => (HttpStatusCode.Forbidden, "You don't have permission to do that."),
        ArgumentException or FormatException => (HttpStatusCode.BadRequest, "The request contained invalid data."),
        InvalidOperationException => (HttpStatusCode.Conflict, ex.Message),
        Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException =>
            (HttpStatusCode.Conflict, "This record was changed by someone else. Please refresh and try again."),
        Microsoft.EntityFrameworkCore.DbUpdateException =>
            (HttpStatusCode.Conflict, "The change couldn't be saved because it conflicts with existing data."),
        TaskCanceledException or OperationCanceledException =>
            (HttpStatusCode.RequestTimeout, "The request took too long and was cancelled."),
        _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again.")
    };
}
