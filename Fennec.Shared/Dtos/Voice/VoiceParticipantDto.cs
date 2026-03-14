namespace Fennec.Shared.Dtos.Voice;

public class VoiceParticipantDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public string? InstanceUrl { get; set; }
}
