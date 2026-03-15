namespace Fennec.Shared.Dtos.Federation;

public class FederationPresencePushRequestDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public bool IsOnline { get; set; }
}
