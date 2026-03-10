using System.Net.Http.Json;
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
        var uri = new Uri("api/v1/server/create", UriKind.Relative);
        
        var response = await httpClient.PostAsJsonAsync(uri, requestDto, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var responseDto = await response.Content.ReadFromJsonAsync<CreateServerResponseDto>(cancellationToken: cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task JoinServerAsync(JoinServerRequestDto requestDto, CancellationToken cancellationToken = default)
    {
        var uri = new Uri("api/v1/server/join", UriKind.Relative);

        var response = await httpClient.PostAsJsonAsync(uri, requestDto, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}