namespace Fennec.Shared.Dtos.Voice;

public class FederationVoiceParticipantEventDto
{
    public Guid ServerId { get; set; }
    public Guid ChannelId { get; set; }
    public VoiceParticipantDto Participant { get; set; } = new();
}
