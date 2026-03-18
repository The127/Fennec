using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Domain;
using Fennec.App.Messages;
using Fennec.Shared.Dtos.Server;
using ShadUI;

namespace Fennec.App.ViewModels;

public partial class ServerPresenceViewModel : ObservableObject,
    IRecipient<UserOnlineMessage>,
    IRecipient<UserOfflineMessage>
{
    private readonly Guid _serverId;
    private readonly ToastManager _toastManager;

    private readonly Dictionary<Guid, (string Username, string? InstanceUrl)> _onlineUsers = new();
    private List<ListServerMembersResponseItemDto> _allMembers = [];

    public ServerPresenceViewModel(Guid serverId, IMessenger messenger, ToastManager toastManager)
    {
        _serverId = serverId;
        _toastManager = toastManager;

        messenger.Register<UserOnlineMessage>(this);
        messenger.Register<UserOfflineMessage>(this);
    }

    public List<string> ServerMembers { get; private set; } = [];

    public ObservableCollection<MemberItem> OnlineMembers { get; } = [];
    public ObservableCollection<MemberItem> OfflineMembers { get; } = [];

    [ObservableProperty]
    private int _onlineMemberCount;

    [ObservableProperty]
    private int _offlineMemberCount;

    public void SetMembers(List<ListServerMembersResponseItemDto> members)
    {
        _allMembers = members;
        ServerMembers = members.Select(m => m.Name).ToList();
    }

    public void SetPresence(IEnumerable<ServerPresenceEntryDto> entries)
    {
        _onlineUsers.Clear();
        foreach (var entry in entries)
            _onlineUsers[entry.UserId] = (entry.Username, entry.InstanceUrl);
        RebuildMemberLists();
    }

    private void RebuildMemberLists()
    {
        OnlineMembers.Clear();
        OfflineMembers.Clear();

        var onlineKeys = new HashSet<string>(_onlineUsers.Values.Select(p => MemberKey(p.Username, p.InstanceUrl)));

        foreach (var member in _allMembers)
        {
            var key = MemberKey(member.Name, member.InstanceUrl);
            if (onlineKeys.Contains(key))
                OnlineMembers.Add(new MemberItem(member.Name, member.InstanceUrl, true));
            else
                OfflineMembers.Add(new MemberItem(member.Name, member.InstanceUrl, false));
        }

        OnlineMemberCount = OnlineMembers.Count;
        OfflineMemberCount = OfflineMembers.Count;
    }

    private static string MemberKey(string name, string? instanceUrl) =>
        new FederatedAddress(name, instanceUrl).ToString();

    [RelayCommand]
    private void AddFriend()
    {
        _toastManager.CreateToast("Not Implemented")
            .WithContent("Adding friends is not yet available.")
            .WithDelay(3)
            .ShowWarning();
    }

    [RelayCommand]
    private void SendDirectMessage()
    {
        _toastManager.CreateToast("Not Implemented")
            .WithContent("Direct messages are not yet available.")
            .WithDelay(3)
            .ShowWarning();
    }

    public void Receive(UserOnlineMessage message)
    {
        if (message.ServerId != _serverId) return;

        Dispatcher.UIThread.Post(() =>
        {
            _onlineUsers[message.UserId] = (message.Username, message.InstanceUrl);
            RebuildMemberLists();
        });
    }

    public void Receive(UserOfflineMessage message)
    {
        if (message.ServerId != _serverId) return;

        Dispatcher.UIThread.Post(() =>
        {
            _onlineUsers.Remove(message.UserId);
            RebuildMemberLists();
        });
    }
}
