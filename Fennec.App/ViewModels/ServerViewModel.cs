using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.Client;
using Fennec.Shared.Dtos.Server;
using Fennec.Shared.Models;

namespace Fennec.App.ViewModels;

public class ChannelItem(Guid id, string name, ChannelType channelType, Guid channelGroupId)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public ChannelType ChannelType { get; } = channelType;
    public Guid ChannelGroupId { get; } = channelGroupId;
    public bool IsTextOnly => ChannelType == ChannelType.TextOnly;
}

public class ChannelGroupItem(Guid id, string name, List<ChannelItem> channels)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public List<ChannelItem> Channels { get; } = channels;
}

public class MessageItem
{
    public required Guid MessageId { get; init; }
    public required string Content { get; init; }
    public required Guid AuthorId { get; init; }
    public required string AuthorName { get; init; }
    public required string AvatarFallback { get; init; }
    public required string CreatedAt { get; init; }
    public required bool ShowAuthor { get; init; }
}

public partial class ServerViewModel(IFennecClient client, Guid serverId, string serverName) : ObservableObject
{
    [ObservableProperty]
    private string _serverName = serverName;

    [ObservableProperty]
    private ChannelItem? _selectedChannel;

    [ObservableProperty]
    private string _messageText = "";

    public Guid ServerId { get; } = serverId;

    public ObservableCollection<ChannelGroupItem> ChannelGroups { get; } = [];

    public ObservableCollection<MessageItem> Messages { get; } = [];

    [RelayCommand]
    private async Task SelectChannel(ChannelItem channel)
    {
        SelectedChannel = channel;
        await LoadMessagesAsync();
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        if (SelectedChannel is null || string.IsNullOrWhiteSpace(MessageText))
            return;

        var content = MessageText.Trim();
        MessageText = "";

        try
        {
            await client.Server.SendMessageAsync(ServerId, SelectedChannel.Id, new SendMessageRequestDto
            {
                Content = content,
            });

            await LoadMessagesAsync();
        }
        catch
        {
            // Message send failed — restore text so user can retry.
            MessageText = content;
        }
    }

    private async Task LoadMessagesAsync()
    {
        if (SelectedChannel is null)
            return;

        try
        {
            var response = await client.Server.ListMessagesAsync(ServerId, SelectedChannel.Id);

            Messages.Clear();

            Guid? lastAuthorId = null;
            foreach (var msg in response.Messages)
            {
                var showAuthor = msg.AuthorId != lastAuthorId;
                lastAuthorId = msg.AuthorId;

                Messages.Add(new MessageItem
                {
                    MessageId = msg.MessageId,
                    Content = msg.Content,
                    AuthorId = msg.AuthorId,
                    AuthorName = msg.AuthorName,
                    AvatarFallback = msg.AuthorName.Length > 0 ? msg.AuthorName[..1].ToUpper() : "?",
                    CreatedAt = msg.CreatedAt,
                    ShowAuthor = showAuthor,
                });
            }
        }
        catch
        {
            // Failed to load messages.
        }
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
                    .Select(c => new ChannelItem(c.ChannelId, c.Name, c.ChannelType, c.ChannelGroupId))
                    .ToList();

                ChannelGroups.Add(new ChannelGroupItem(group.ChannelGroupId, group.Name, channels));
            }

            var firstChannel = ChannelGroups.FirstOrDefault()?.Channels.FirstOrDefault();
            if (firstChannel is not null)
            {
                await SelectChannel(firstChannel);
            }
        }
        catch
        {
            // Server unreachable — channels stay empty.
        }
    }
}
