namespace Fennec.App.Exceptions;

public class NullExceptionHandler : IExceptionHandler
{
    public static NullExceptionHandler Instance { get; } = new();
    public void Handle(Exception exception, string? message = null, params object?[] args) { }
}
