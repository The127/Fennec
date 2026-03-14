namespace Fennec.Shared.Dtos.Server;

public class ListServerMembersResponseItemDto
{
    public required string Name { get; init; }
    public string? InstanceUrl { get; init; }
}
