namespace Fennec.Shared.Dtos.Voice;

public class FederationScreenShareEventDto
{
    public required Guid ServerId { get; set; }
    public required Guid ChannelId { get; set; }
    public required Guid UserId { get; set; }
    public required string Username { get; set; }
    public string? InstanceUrl { get; set; }
    public required bool IsSharing { get; set; }
}
