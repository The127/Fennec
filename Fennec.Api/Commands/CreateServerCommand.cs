using Fennec.Api.Models;
using MediatR;

namespace Fennec.Api.Commands;

public record CreateServerCommand : IRequest
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
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
        };
        
        await dbContext.AddAsync(server, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}