namespace Fennec.Shared.Dtos.Voice;

public class ScreenShareInfoDto
{
    public required Guid UserId { get; set; }
    public required string Username { get; set; }
    public string? InstanceUrl { get; set; }
}
