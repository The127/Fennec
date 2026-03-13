using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Exceptions;
using Fennec.App.Messages;
using Fennec.App.Routing;
using Fennec.Client;
using Fennec.Shared.Dtos.Server;
namespace Fennec.App.ViewModels;

public partial class JoinServerViewModel(
    IFennecClient client,
    IRouter router,
    IMessenger messenger,
    string homeInstanceUrl,
    IExceptionHandler exceptionHandler
) : ObservableObject
{
    [ObservableProperty]
    private string _inviteLink = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isJoining;

    [RelayCommand]
    private async Task JoinServerAsync()
    {
        if (string.IsNullOrWhiteSpace(InviteLink))
        {
            ErrorMessage = "Please enter an invite link.";
            return;
        }

        if (!Uri.TryCreate(InviteLink.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && uri.Scheme != "http"))
        {
            ErrorMessage = "Invalid invite link format.";
            return;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length != 2 || segments[0] != "invite" || string.IsNullOrEmpty(segments[1]))
        {
            ErrorMessage = "Link must be in the format https://{instance}/invite/{code}";
            return;
        }

        var targetInstanceUrl = uri.Authority;
        var inviteCode = segments[1];

        IsJoining = true;
        ErrorMessage = null;

        try
        {
            await client.Server.JoinServerAsync(homeInstanceUrl, new JoinServerRequestDto
            {
                InviteCode = inviteCode,
                InstanceUrl = targetInstanceUrl,
            });

            messenger.Send(new ServerJoinedMessage());
            await router.NavigateBackAsync();
        }
        catch (Exception ex)
        {
            exceptionHandler.Handle(ex, "Failed to join server from invite {InviteLink} on {HomeInstanceUrl}", InviteLink, homeInstanceUrl);
            ErrorMessage = $"Failed to join server: {ex.Message}";
        }
        finally
        {
            IsJoining = false;
        }
    }
}
