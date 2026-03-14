namespace Fennec.Shared.Dtos.Voice;

public class FederationVoiceLeaveRequestDto
{
    public Guid ServerId { get; set; }
    public Guid ChannelId { get; set; }
    public Guid UserId { get; set; }
}
