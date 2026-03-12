using System.Net.Http.Json;
using Fennec.Shared;
using Fennec.Shared.Dtos.Auth;

namespace Fennec.Client.Clients;

public interface IAuthClient
{
    Task RegisterAsync(string baseUrl, RegisterUserRequestDto request, CancellationToken cancellationToken = default);
    Task<LoginResponseDto> LoginAsync(string baseUrl, LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<GetPublicTokenResponseDto> GetPublicTokenAsync(string baseUrl, GetPublicTokenRequestDto request, CancellationToken cancellationToken = default);
    Task LogoutAsync(string baseUrl, CancellationToken cancellationToken = default);
}

public class AuthClient(HttpClient httpClient) : IAuthClient
{
    public async Task RegisterAsync(string baseUrl, RegisterUserRequestDto request, CancellationToken cancellationToken = default)
    {
        baseUrl = UrlUtils.NormalizeBaseUrl(baseUrl);
        var uri = new Uri($"{baseUrl}/api/v1/auth/register");
        
        var response = await httpClient.PostAsJsonAsync(uri, request, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);
    }

    public async Task<LoginResponseDto> LoginAsync(string baseUrl, LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        baseUrl = UrlUtils.NormalizeBaseUrl(baseUrl);
        var uri = new Uri($"{baseUrl}/api/v1/auth/login");
        
        var response = await httpClient.PostAsJsonAsync(
            uri,
            request,
            SharedFennecJsonContext.Default.LoginRequestDto,
            cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);
        
        var responseDto = await response.Content.ReadFromJsonAsync<LoginResponseDto>(
            SharedFennecJsonContext.Default.LoginResponseDto,
            cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task<GetPublicTokenResponseDto> GetPublicTokenAsync(string baseUrl, GetPublicTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        baseUrl = UrlUtils.NormalizeBaseUrl(baseUrl);
        var uri = new Uri($"{baseUrl}/api/v1/auth/public-token");

        var response = await httpClient.PostAsJsonAsync(
            uri,
            request,
            SharedFennecJsonContext.Default.GetPublicTokenRequestDto,
            cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);

        var responseDto = await response.Content.ReadFromJsonAsync<GetPublicTokenResponseDto>(
            SharedFennecJsonContext.Default.GetPublicTokenResponseDto,
            cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }

    public async Task LogoutAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        baseUrl = UrlUtils.NormalizeBaseUrl(baseUrl);
        var uri = new Uri($"{baseUrl}/api/v1/auth/logout");

        var response = await httpClient.PostAsync(uri, null, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);
    }
}
