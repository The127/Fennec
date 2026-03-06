using Fennec.Api.Models;
using Fennec.Api.Security;
using MediatR;

namespace Fennec.Api.Commands;

public record CreateServerCommand : IRequest
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required ServerVisibility Visibility { get; init; }
    public required IAuthPrinciple AuthPrinciple { get; init; }
}

public class CreateServerCommandHandler(
    FennecDbContext dbContext
) : IRequestHandler<CreateServerCommand>
{
    public async Task Handle(CreateServerCommand request, CancellationToken cancellationToken)
    {
        var server = new Server
        {
            Name = request.Name,
            Slug = request.Slug,
            Visibility = request.Visibility,
        };
        
        var member = new ServerMember
        {
            UserId = request.AuthPrinciple.Id,
            ServerId = server.Id,
        };
        
        dbContext.AddRange(server, member);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}