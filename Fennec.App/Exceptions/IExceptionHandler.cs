namespace Fennec.App.Exceptions;

public interface IExceptionHandler
{
    void Handle(Exception exception, string? message = null, params object?[] args);
}
