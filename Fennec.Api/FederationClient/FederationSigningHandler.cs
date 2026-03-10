using Fennec.Api.Services;
using Fennec.Api.Settings;
using Microsoft.Extensions.Options;

namespace Fennec.Api.FederationClient;

public class FederationSigningHandler(IKeyService keyService, IOptions<FennecSettings> fennecOptions) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("X-Instance", fennecOptions.Value.IssuerUrl);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        request.Headers.Add("X-Timestamp", timestamp.ToString());

        var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : "";
        var payload = $"{request.Method}\n{request.RequestUri?.PathAndQuery}\n{timestamp}\n{body}";
        
        request.Headers.Add("X-Signature", keyService.SignPayload(payload));

        return await base.SendAsync(request, cancellationToken);
    }
}