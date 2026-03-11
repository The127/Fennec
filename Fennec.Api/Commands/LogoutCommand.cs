using Fennec.Api.Models;
using Fennec.Shared.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public class LogoutCommand : IRequest
{
    public required SessionToken Token { get; init; }
}

public class LogoutCommandHandler(
    FennecDbContext dbContext    
) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var session = await dbContext.Set<Session>()
            .Where(x => x.Token == request.Token.Value)
            .SingleOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return;
        }
        
        dbContext.Remove(session);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}