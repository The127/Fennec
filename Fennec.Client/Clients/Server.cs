using System.Net.Http.Json;
using Fennec.Shared;
using Fennec.Shared.Dtos.Server;

namespace Fennec.Client.Clients;

public interface IServerClient
{
    Task<CreateServerResponseDto> CreateServerAsync(CreateServerRequestDto requestDto, CancellationToken cancellationToken = default);
    Task JoinServerAsync(JoinServerRequestDto requestDto, CancellationToken cancellationToken = default);
    Task<ListJoinedServersResponseDto> ListJoinedServersAsync(CancellationToken cancellationToken = default);
    Task<CreateServerInviteResponseDto> CreateInviteAsync(Guid serverId, CreateServerInviteRequestDto requestDto, CancellationToken cancellationToken = default);
    Task<ListServerInvitesResponseDto> ListInvitesAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task DeleteInviteAsync(Guid serverId, Guid inviteId, CancellationToken cancellationToken = default);
    Task<CreateChannelGroupResponseDto> CreateChannelGroupAsync(Guid serverId, CreateChannelGroupRequestDto requestDto, CancellationToken cancellationToken = default);
    Task<ListChannelGroupsResponseDto> ListChannelGroupsAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task RenameChannelGroupAsync(Guid serverId, Guid channelGroupId, RenameChannelGroupRequestDto requestDto, CancellationToken cancellationToken = default);
    Task DeleteChannelGroupAsync(Guid serverId, Guid channelGroupId, CancellationToken cancellationToken = default);
    Task<CreateChannelResponseDto> CreateChannelAsync(Guid serverId, Guid channelGroupId, CreateChannelRequestDto requestDto, CancellationToken cancellationToken = default);
    Task<ListChannelsResponseDto> ListChannelsAsync(Guid serverId, Guid channelGroupId, CancellationToken cancellationToken = default);
    Task RenameChannelAsync(Guid serverId, Guid channelGroupId, Guid channelId, RenameChannelRequestDto requestDto, CancellationToken cancellationToken = default);
    Task UpdateChannelTypeAsync(Guid serverId, Guid channelGroupId, Guid channelId, UpdateChannelTypeRequestDto requestDto, CancellationToken cancellationToken = default);
    Task DeleteChannelAsync(Guid serverId, Guid channelGroupId, Guid channelId, CancellationToken cancellationToken = default);
    Task<SendMessageResponseDto> SendMessageAsync(Guid serverId, Guid channelId, SendMessageRequestDto requestDto, CancellationToken cancellationToken = default);
    Task<ListMessagesResponseDto> ListMessagesAsync(Guid serverId, Guid channelId, CancellationToken cancellationToken = default);
}

public class ServerClient(HttpClient httpClient) : IServerClient
{
    public async Task<CreateServerResponseDto> CreateServerAsync(CreateServerRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var uri = new Uri("api/v1/servers/create", UriKind.Relative);
        
        var response = await httpClient.PostAsJsonAsync(
            uri, 
            requestDto, 
            SharedFennecJsonContext.Default.CreateServerRequestDto, 
            cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);
        
        var responseDto = await response.Content.ReadFromJsonAsync<CreateServerResponseDto>(
            SharedFennecJsonContext.Default.CreateServerResponseDto,
            cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task JoinServerAsync(JoinServerRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var uri = new Uri("api/v1/servers/join", UriKind.Relative);

        var response = await httpClient.PostAsJsonAsync(
            uri,
            requestDto,
            SharedFennecJsonContext.Default.JoinServerRequestDto,
            cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);
    }

    public async Task<ListJoinedServersResponseDto> ListJoinedServersAsync(CancellationToken cancellationToken = default)
    {
        var uri = new Uri("api/v1/servers/joined", UriKind.Relative);

        var response = await httpClient.GetAsync(uri, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);

        var responseDto = await response.Content.ReadFromJsonAsync(
            SharedFennecJsonContext.Default.ListJoinedServersResponseDto,
            cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task<CreateServerInviteResponseDto> CreateInviteAsync(Guid serverId, CreateServerInviteRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/invites", UriKind.Relative);

        var response = await httpClient.PostAsJsonAsync(
            uri,
            requestDto,
            SharedFennecJsonContext.Default.CreateServerInviteRequestDto,
            cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);

        var responseDto = await response.Content.ReadFromJsonAsync(
            SharedFennecJsonContext.Default.CreateServerInviteResponseDto,
            cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task<ListServerInvitesResponseDto> ListInvitesAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/invites", UriKind.Relative);

        var response = await httpClient.GetAsync(uri, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);

        var responseDto = await response.Content.ReadFromJsonAsync(
            SharedFennecJsonContext.Default.ListServerInvitesResponseDto,
            cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task DeleteInviteAsync(Guid serverId, Guid inviteId, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/invites/{inviteId}", UriKind.Relative);

        var response = await httpClient.DeleteAsync(uri, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);
    }

    public async Task<CreateChannelGroupResponseDto> CreateChannelGroupAsync(Guid serverId, CreateChannelGroupRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/channel-groups", UriKind.Relative);

        var response = await httpClient.PostAsJsonAsync(uri, requestDto, SharedFennecJsonContext.Default.CreateChannelGroupRequestDto, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);

        var responseDto = await response.Content.ReadFromJsonAsync(SharedFennecJsonContext.Default.CreateChannelGroupResponseDto, cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task<ListChannelGroupsResponseDto> ListChannelGroupsAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/channel-groups", UriKind.Relative);

        var response = await httpClient.GetAsync(uri, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);

        var responseDto = await response.Content.ReadFromJsonAsync(SharedFennecJsonContext.Default.ListChannelGroupsResponseDto, cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task RenameChannelGroupAsync(Guid serverId, Guid channelGroupId, RenameChannelGroupRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/channel-groups/{channelGroupId}/name", UriKind.Relative);

        var response = await httpClient.PutAsJsonAsync(uri, requestDto, SharedFennecJsonContext.Default.RenameChannelGroupRequestDto, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);
    }

    public async Task DeleteChannelGroupAsync(Guid serverId, Guid channelGroupId, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/channel-groups/{channelGroupId}", UriKind.Relative);

        var response = await httpClient.DeleteAsync(uri, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);
    }

    public async Task<CreateChannelResponseDto> CreateChannelAsync(Guid serverId, Guid channelGroupId, CreateChannelRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/channel-groups/{channelGroupId}/channels", UriKind.Relative);

        var response = await httpClient.PostAsJsonAsync(uri, requestDto, SharedFennecJsonContext.Default.CreateChannelRequestDto, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);

        var responseDto = await response.Content.ReadFromJsonAsync(SharedFennecJsonContext.Default.CreateChannelResponseDto, cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task<ListChannelsResponseDto> ListChannelsAsync(Guid serverId, Guid channelGroupId, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/channel-groups/{channelGroupId}/channels", UriKind.Relative);

        var response = await httpClient.GetAsync(uri, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);

        var responseDto = await response.Content.ReadFromJsonAsync(SharedFennecJsonContext.Default.ListChannelsResponseDto, cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task RenameChannelAsync(Guid serverId, Guid channelGroupId, Guid channelId, RenameChannelRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/channel-groups/{channelGroupId}/channels/{channelId}/name", UriKind.Relative);

        var response = await httpClient.PutAsJsonAsync(uri, requestDto, SharedFennecJsonContext.Default.RenameChannelRequestDto, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);
    }

    public async Task UpdateChannelTypeAsync(Guid serverId, Guid channelGroupId, Guid channelId, UpdateChannelTypeRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/channel-groups/{channelGroupId}/channels/{channelId}/type", UriKind.Relative);

        var response = await httpClient.PutAsJsonAsync(uri, requestDto, SharedFennecJsonContext.Default.UpdateChannelTypeRequestDto, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);
    }

    public async Task DeleteChannelAsync(Guid serverId, Guid channelGroupId, Guid channelId, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/channel-groups/{channelGroupId}/channels/{channelId}", UriKind.Relative);

        var response = await httpClient.DeleteAsync(uri, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);
    }

    public async Task<SendMessageResponseDto> SendMessageAsync(Guid serverId, Guid channelId, SendMessageRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/channels/{channelId}/messages", UriKind.Relative);

        var response = await httpClient.PostAsJsonAsync(uri, requestDto, SharedFennecJsonContext.Default.SendMessageRequestDto, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);

        var responseDto = await response.Content.ReadFromJsonAsync(SharedFennecJsonContext.Default.SendMessageResponseDto, cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task<ListMessagesResponseDto> ListMessagesAsync(Guid serverId, Guid channelId, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/channels/{channelId}/messages", UriKind.Relative);

        var response = await httpClient.GetAsync(uri, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);

        var responseDto = await response.Content.ReadFromJsonAsync(SharedFennecJsonContext.Default.ListMessagesResponseDto, cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }
}