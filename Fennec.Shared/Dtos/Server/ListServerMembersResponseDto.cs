namespace Fennec.Shared.Dtos.Server;

public class ListServerMembersResponseDto
{
    public required List<ListServerMembersResponseItemDto> Members { get; init; }
}
