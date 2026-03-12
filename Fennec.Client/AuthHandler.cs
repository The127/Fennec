using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fennec.Shared;
using Fennec.Shared.Dtos.Auth;

namespace Fennec.Client;

internal class AuthHandler(ITokenStore tokenStore) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("Request URI is missing");
        var baseUrl = $"{uri.Scheme}://{uri.Authority}";
        var path = uri.AbsolutePath;

        tokenStore.UpdateLastUsed(baseUrl);

        var isHome = baseUrl == tokenStore.HomeUrl;
        var useSession = isHome && (
            path.Contains("/auth/public-token") ||
            path.Contains("/auth/logout") ||
            path.Contains("/user/me")
        );

        if (useSession)
        {
            if (tokenStore.HomeSessionToken != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Session", tokenStore.HomeSessionToken);
            }
        }
        else
        {
            var bearerToken = tokenStore.GetPublicToken(baseUrl);
            
            // Lazy-loading: if bearer token is missing and we have a home session, fetch it
            if (bearerToken == null && tokenStore.HomeUrl != null && tokenStore.HomeSessionToken != null)
            {
                bearerToken = await FetchPublicTokenAsync(baseUrl, cancellationToken);
                if (bearerToken != null)
                {
                    tokenStore.SetPublicToken(baseUrl, bearerToken);
                }
            }

            if (bearerToken != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string?> FetchPublicTokenAsync(string targetUrl, CancellationToken cancellationToken)
    {
        if (tokenStore.HomeUrl == null || tokenStore.HomeSessionToken == null) return null;

        var publicTokenUri = new Uri($"{UrlUtils.NormalizeBaseUrl(tokenStore.HomeUrl!)}/api/v1/auth/public-token");
        var publicTokenRequest = new HttpRequestMessage(HttpMethod.Post, publicTokenUri)
        {
            Content = JsonContent.Create(new GetPublicTokenRequestDto
            {
                Audience = targetUrl
            }, SharedFennecJsonContext.Default.GetPublicTokenRequestDto)
        };

        publicTokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Session", tokenStore.HomeSessionToken);

        var response = await base.SendAsync(publicTokenRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<GetPublicTokenResponseDto>(
            SharedFennecJsonContext.Default.GetPublicTokenResponseDto,
            cancellationToken);

        return result?.Token;
    }
}
