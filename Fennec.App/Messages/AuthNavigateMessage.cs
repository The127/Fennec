namespace Fennec.App.Messages;

public enum AuthNavigationTarget
{
    Login,
    Register,
}

public record AuthNavigateMessage(AuthNavigationTarget Target);