namespace Fennec.App.Messages;

public enum AuthNavigationTarget
{
    Login,
    Register,
}

public record AuthNavigationMessage(AuthNavigationTarget Target);