using Microsoft.Extensions.Logging;

namespace Fennec.App.Logger;

public class ConsoleLogger : ILogger
{
    private readonly string _category;

    public ConsoleLogger(string category)
    {
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        // No scoping needed for simple console logging
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        // Enable all levels for simplicity
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var level = logLevel.ToString().ToUpper();

        if (exception != null)
        {
            message += $" Exception: {exception}";
        }

        Console.WriteLine($"[{level}] {_category}: {message}");
    }
}