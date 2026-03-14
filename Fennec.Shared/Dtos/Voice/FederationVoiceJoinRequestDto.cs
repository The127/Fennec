namespace Fennec.Shared.Dtos.Voice;

public class FederationVoiceJoinRequestDto
{
    public Guid ServerId { get; set; }
    public Guid ChannelId { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public string CallerInstanceUrl { get; set; } = "";
}
