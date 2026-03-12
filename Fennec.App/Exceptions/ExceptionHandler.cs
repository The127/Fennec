using Microsoft.Extensions.Logging;

namespace Fennec.App.Exceptions;

public class ExceptionHandler(ILogger<ExceptionHandler> logger) : IExceptionHandler
{
    public void Handle(Exception exception, string? message = null, params object?[] args)
    {
        if (message != null)
        {
            logger.LogError(exception, message, args);
        }
        else
        {
            logger.LogError(exception, "An unhandled exception occurred");
        }
    }
}
