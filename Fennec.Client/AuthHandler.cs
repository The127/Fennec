using System.Net.Http.Headers;

namespace Fennec.Client;

internal class AuthHandler(TokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath;

        var useSession = path != null && (
            path.EndsWith("/auth/public-token") ||
            path.EndsWith("/auth/logout") ||
            path.EndsWith("/users/me")
        );

        request.Headers.Authorization = useSession switch
        {
            true when tokenProvider.SessionToken != null => new AuthenticationHeaderValue("Session",
                tokenProvider.SessionToken),
            false when tokenProvider.BearerToken != null => new AuthenticationHeaderValue("Bearer",
                tokenProvider.BearerToken),
            _ => request.Headers.Authorization
        };

        return await base.SendAsync(request, cancellationToken);
    }
}
