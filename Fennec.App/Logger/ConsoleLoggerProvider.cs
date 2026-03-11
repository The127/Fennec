using Microsoft.Extensions.Logging;

namespace Fennec.App.Logger;

public class ConsoleLoggerProvider : ILoggerProvider
{
    public void Dispose()
    {
        // Nothing to dispose
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new ConsoleLogger(categoryName);
    }
}