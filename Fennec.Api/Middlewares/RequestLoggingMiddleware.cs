using System.Diagnostics;

namespace Fennec.Api.Middlewares;

public class RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("HTTP {Method} {Path}", context.Request.Method, context.Request.Path);

        await next(context);

        stopwatch.Stop();

        logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
            context.Request.Method, context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    }
}
