using Fennec.Shared.Dtos.Server;
using Microsoft.AspNetCore.SignalR.Client;

namespace Fennec.Client;

public interface IMessageHubClient : IAsyncDisposable
{
    Task ConnectAsync(string baseUrl, string token);
    Task SubscribeToChannelAsync(Guid serverId, Guid channelId);
    Task UnsubscribeFromChannelAsync(Guid serverId, Guid channelId);
    Task DisconnectAsync();
    event Action<Guid, Guid, MessageReceivedDto>? MessageReceived;
}

public class MessageHubClient : IMessageHubClient
{
    private HubConnection? _connection;

    public event Action<Guid, Guid, MessageReceivedDto>? MessageReceived;

    public async Task ConnectAsync(string baseUrl, string token)
    {
        if (_connection is not null)
            await DisconnectAsync();

        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/messages", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<Guid, Guid, MessageReceivedDto>("MessageReceived", (serverId, channelId, message) =>
        {
            MessageReceived?.Invoke(serverId, channelId, message);
        });

        await _connection.StartAsync();
    }

    public async Task SubscribeToChannelAsync(Guid serverId, Guid channelId)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("SubscribeToChannel", serverId, channelId);
    }

    public async Task UnsubscribeFromChannelAsync(Guid serverId, Guid channelId)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("UnsubscribeFromChannel", serverId, channelId);
    }

    public async Task DisconnectAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
