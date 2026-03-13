namespace Fennec.App.Messages;

public enum AuthNavigationTarget
{
    Login,
    Register,
    SwitchAccount,
}

public record AuthNavigationMessage(AuthNavigationTarget Target);