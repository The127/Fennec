namespace Fennec.Shared.Dtos.Voice;

public class FederationVoiceParticipantLeftEventDto
{
    public Guid ServerId { get; set; }
    public Guid ChannelId { get; set; }
    public Guid UserId { get; set; }
}
