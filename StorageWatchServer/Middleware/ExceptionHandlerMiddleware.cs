using StorageWatchServer.Services.Logging;

namespace StorageWatchServer.Middleware;

/// <summary>
/// Global exception handler middleware that logs all unhandled exceptions with [ERROR] tag.
/// </summary>
public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;
    private readonly RollingFileLogger? _rollingLogger;

    public ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger, RollingFileLogger? rollingLogger = null)
    {
        _next = next;
        _logger = logger;
        _rollingLogger = rollingLogger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in request pipeline");
            _rollingLogger?.Log($"[ERROR] Unhandled exception: {ex.Message}");
            
            // Optionally re-throw or handle gracefully
            throw;
        }
    }
}

/// <summary>
/// Extension method to register the exception handler middleware.
/// </summary>
public static class ExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandlerMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlerMiddleware>();
    }
}
