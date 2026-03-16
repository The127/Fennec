namespace Fennec.Shared.Dtos.Voice;

public class VoiceParticipantDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public string? InstanceUrl { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsScreenSharing { get; set; }
}
