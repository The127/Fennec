using Fennec.Shared.Dtos.Voice;

namespace Fennec.Api.FederationClient.Clients;

public interface IVoiceClient
{
    Task<FederationVoiceJoinResponseDto> JoinAsync(FederationVoiceJoinRequestDto request, CancellationToken cancellationToken = default);
    Task LeaveAsync(FederationVoiceLeaveRequestDto request, CancellationToken cancellationToken = default);
    Task NotifyParticipantJoinedAsync(FederationVoiceParticipantEventDto request, CancellationToken cancellationToken = default);
    Task NotifyParticipantLeftAsync(FederationVoiceParticipantLeftEventDto request, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, List<VoiceParticipantDto>>> GetVoiceStateAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task NotifyScreenShareAsync(FederationScreenShareEventDto request, CancellationToken cancellationToken = default);
}

public class VoiceClient(HttpClient httpClient, string instanceUrl) : IVoiceClient
{
    private Uri BuildUri(string path)
    {
        var baseUri = instanceUrl.EndsWith('/') ? new Uri(instanceUrl) : new Uri(instanceUrl + "/");
        return new Uri(baseUri, path);
    }

    public async Task<FederationVoiceJoinResponseDto> JoinAsync(FederationVoiceJoinRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BuildUri("federation/v1/voice/join"), request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FederationVoiceJoinResponseDto>(cancellationToken) ?? new();
    }

    public async Task LeaveAsync(FederationVoiceLeaveRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BuildUri("federation/v1/voice/leave"), request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task NotifyParticipantJoinedAsync(FederationVoiceParticipantEventDto request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BuildUri("federation/v1/voice/participant-joined"), request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task NotifyParticipantLeftAsync(FederationVoiceParticipantLeftEventDto request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BuildUri("federation/v1/voice/participant-left"), request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Dictionary<Guid, List<VoiceParticipantDto>>> GetVoiceStateAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(BuildUri($"federation/v1/voice/state/{serverId}"), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Dictionary<Guid, List<VoiceParticipantDto>>>(cancellationToken) ?? new();
    }

    public async Task NotifyScreenShareAsync(FederationScreenShareEventDto request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(BuildUri("federation/v1/voice/screen-share"), request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
