using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Routing;
using Fennec.App.Services;
using Fennec.App.ViewModels;
using Fennec.Client;
using ShadUI;

namespace Fennec.App.Routes;

public record ServerRoute(IFennecClient Client, DialogManager DialogManager, IServerStore ServerStore, IMessageHubService MessageHubService, IVoiceCallService VoiceCallService, IMessenger Messenger, Guid ServerId, string ServerName, string InstanceUrl, string CurrentUsername) : IRoute
{
    public ObservableObject GetViewModel(IServiceProvider serviceProvider)
    {
        var vm = new ServerViewModel(Client, DialogManager, ServerStore, MessageHubService, VoiceCallService, Messenger, ServerId, ServerName, InstanceUrl, CurrentUsername);
        _ = vm.LoadAsync();
        return vm;
    }
}
