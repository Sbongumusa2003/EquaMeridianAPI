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
            // Always include exception type + message for Swagger generation failures so
            // deploy issues are diagnosable without enabling full Development mode.
            var isSwagger = context.Request.Path.StartsWithSegments("/swagger");
            var body = new
            {
                message = friendlyMessage,
                statusCode = (int)statusCode,
                traceId = context.TraceIdentifier,
                detail = isDev ? ex.ToString()
                    : isSwagger ? DescribeExceptionChain(ex)
                    : null
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }

    // SwaggerGeneratorException's own Message is just "Failed to generate Operation
    // for action - X. See inner exception" — the actual cause is always in
    // InnerException. Walk the whole chain so a swagger 500 is self-describing
    // without needing to flip on full Development mode.
    private static string DescribeExceptionChain(Exception ex)
    {
        var parts = new List<string>();
        var current = ex;
        while (current != null)
        {
            parts.Add($"{current.GetType().Name}: {current.Message}");
            current = current.InnerException;
        }
        return string.Join(" ---> ", parts);
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
