using System.Net.Http.Json;
using Fennec.Shared;
using Fennec.Shared.Dtos.User;

namespace Fennec.Client.Clients;

public interface IUserClient
{
    Task<MeResponseDto> GetMeAsync(CancellationToken cancellationToken = default);
}

public class UserClient(HttpClient httpClient) : IUserClient
{
    public async Task<MeResponseDto> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var uri = new Uri("api/v1/user/me", UriKind.Relative);
        
        var response = await httpClient.GetAsync(
            uri,
            cancellationToken);
        await response.EnsureSuccessAsync(cancellationToken);
        
        var responseDto = await response.Content.ReadFromJsonAsync<MeResponseDto>(
            SharedFennecJsonContext.Default.MeResponseDto,
            cancellationToken);
        return responseDto ?? throw new Exception("Error decoding response.");
    }
}