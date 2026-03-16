using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Routing;
using Fennec.App.Services;
using Fennec.App.ViewModels;
using Fennec.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadUI;

namespace Fennec.App.Routes;

public record ServerRoute(IFennecClient Client, DialogManager DialogManager, IServerStore ServerStore, IMessageHubService MessageHubService, IVoiceCallService VoiceCallService, IMessenger Messenger, ToastManager ToastManager, ISettingsStore SettingsStore, Guid ServerId, string ServerName, string InstanceUrl, Guid CurrentUserId, string CurrentUsername) : IRoute
{
    public ObservableObject GetViewModel(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ServerViewModel>>();
        var vm = new ServerViewModel(Client, DialogManager, ServerStore, MessageHubService, VoiceCallService, Messenger, ToastManager, logger, SettingsStore, ServerId, ServerName, InstanceUrl, CurrentUserId, CurrentUsername);
        _ = vm.LoadAsync();
        return vm;
    }
}
