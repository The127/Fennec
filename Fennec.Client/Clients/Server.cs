using System.Net.Http.Json;
using Fennec.Shared;
using Fennec.Shared.Dtos.Server;

namespace Fennec.Client.Clients;

public interface IServerClient
{
    Task<CreateServerResponseDto> CreateServerAsync(CreateServerRequestDto requestDto, CancellationToken cancellationToken = default);
    Task JoinServerAsync(JoinServerRequestDto requestDto, CancellationToken cancellationToken = default);
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
}