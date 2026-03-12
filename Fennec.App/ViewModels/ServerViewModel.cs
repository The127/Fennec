using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.Client;
using Fennec.Shared.Models;

namespace Fennec.App.ViewModels;

public class ChannelItem(Guid id, string name, ChannelType channelType)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public ChannelType ChannelType { get; } = channelType;
    public bool IsTextOnly => ChannelType == ChannelType.TextOnly;
}

public class ChannelGroupItem(Guid id, string name, List<ChannelItem> channels)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public List<ChannelItem> Channels { get; } = channels;
}

public partial class ServerViewModel(IFennecClient client, Guid serverId, string serverName) : ObservableObject
{
    [ObservableProperty]
    private string _serverName = serverName;

    [ObservableProperty]
    private ChannelItem? _selectedChannel;

    public Guid ServerId { get; } = serverId;

    public ObservableCollection<ChannelGroupItem> ChannelGroups { get; } = [];

    [RelayCommand]
    private void SelectChannel(ChannelItem channel)
    {
        SelectedChannel = channel;
    }

    public async Task LoadAsync()
    {
        try
        {
            var groupsResponse = await client.Server.ListChannelGroupsAsync(ServerId);

            ChannelGroups.Clear();

            foreach (var group in groupsResponse.ChannelGroups)
            {
                var channelsResponse = await client.Server.ListChannelsAsync(ServerId, group.ChannelGroupId);

                var channels = channelsResponse.Channels
                    .Select(c => new ChannelItem(c.ChannelId, c.Name, c.ChannelType))
                    .ToList();

                ChannelGroups.Add(new ChannelGroupItem(group.ChannelGroupId, group.Name, channels));
            }

            SelectedChannel = ChannelGroups.FirstOrDefault()?.Channels.FirstOrDefault();
        }
        catch
        {
            // Server unreachable — channels stay empty.
        }
    }
}
