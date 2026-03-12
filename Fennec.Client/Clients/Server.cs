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
        response.EnsureSuccessStatusCode();
        
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
        response.EnsureSuccessStatusCode();
    }

    public async Task<ListJoinedServersResponseDto> ListJoinedServersAsync(CancellationToken cancellationToken = default)
    {
        var uri = new Uri("api/v1/servers/joined", UriKind.Relative);

        var response = await httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

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
        response.EnsureSuccessStatusCode();

        var responseDto = await response.Content.ReadFromJsonAsync(
            SharedFennecJsonContext.Default.CreateServerInviteResponseDto,
            cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task<ListServerInvitesResponseDto> ListInvitesAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/invites", UriKind.Relative);

        var response = await httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseDto = await response.Content.ReadFromJsonAsync(
            SharedFennecJsonContext.Default.ListServerInvitesResponseDto,
            cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task DeleteInviteAsync(Guid serverId, Guid inviteId, CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"api/v1/servers/{serverId}/invites/{inviteId}", UriKind.Relative);

        var response = await httpClient.DeleteAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}