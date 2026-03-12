using System.Net.Http.Json;
using Fennec.Shared;
using Fennec.Shared.Dtos.Auth;

namespace Fennec.Client.Clients;

public interface IAuthClient
{
    Task RegisterAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default);
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<GetPublicTokenResponseDto> GetPublicTokenAsync(GetPublicTokenRequestDto request, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken);
}

public class AuthClient(HttpClient httpClient) : IAuthClient
{
    public async Task RegisterAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default)
    {
        var uri = new Uri("api/v1/auth/register", UriKind.Relative);
        
        var response = await httpClient.PostAsJsonAsync(uri, request, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var uri = new Uri("api/v1/auth/login", UriKind.Relative);
        
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

    public async Task<GetPublicTokenResponseDto> GetPublicTokenAsync(GetPublicTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var uri = new Uri("api/v1/auth/public-token", UriKind.Relative);

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

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        var uri = new Uri("api/v1/auth/logout", UriKind.Relative);

        var response = await httpClient.PostAsync(uri, null, cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);
    }
}
