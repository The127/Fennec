using System.Net.Http.Json;
using Fennec.Shared.Dtos.Auth;

namespace Fennec.Client.Clients;

public interface IAuthClient
{
    Task RegisterAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default);
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<GetPublicTokenResponseDto> GetPublicTokenAsync(GetPublicTokenRequestDto request, CancellationToken cancellationToken = default);
}

public class AuthClient(HttpClient httpClient) : IAuthClient
{
    public async Task RegisterAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default)
    {
        var uri = new Uri("api/v1/auth/register");
        
        var response = await httpClient.PostAsJsonAsync(uri, request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var uri = new Uri("api/v1/auth/login");
        
        var response = await httpClient.PostAsJsonAsync(uri, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var responseDto = await response.Content.ReadFromJsonAsync<LoginResponseDto>(cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task<GetPublicTokenResponseDto> GetPublicTokenAsync(GetPublicTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var uri = new Uri("api/v1/auth/public-token");

        var response = await httpClient.PostAsJsonAsync(uri, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var responseDto = await response.Content.ReadFromJsonAsync<GetPublicTokenResponseDto>(cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }
}
