using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Exceptions;
using Fennec.App.Messages;
using Fennec.App.Routing;
using Fennec.Client;
using Fennec.Shared.Dtos.Server;
using Fennec.Shared.Models;
namespace Fennec.App.ViewModels;

public partial class CreateServerViewModel(
    IFennecClient client,
    IRouter router,
    IMessenger messenger,
    string instanceUrl,
    IExceptionHandler exceptionHandler
) : ObservableObject
{
    [ObservableProperty]
    private string _serverName = string.Empty;

    [ObservableProperty]
    private bool _isPublic = true;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isCreating;

    [RelayCommand]
    private async Task CreateServerAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerName))
        {
            ErrorMessage = "Server name is required.";
            return;
        }

        IsCreating = true;
        ErrorMessage = null;

        try
        {
            var response = await client.Server.CreateServerAsync(instanceUrl, new CreateServerRequestDto
            {
                Name = ServerName.Trim(),
                Visibility = IsPublic ? ServerVisibility.Public : ServerVisibility.Private,
            });

            messenger.Send(new ServerCreatedMessage(response.ServerId, ServerName.Trim()));
            await router.NavigateBackAsync();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            exceptionHandler.Handle(ex, "Failed to create server {ServerName} on {Url}", ServerName, instanceUrl);
            ErrorMessage = $"Failed to create server: {ex.Message}";
        }
        finally
        {
            IsCreating = false;
        }
    }
}
