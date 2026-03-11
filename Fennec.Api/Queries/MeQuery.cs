
using Fennec.Api.Models;
using Fennec.Shared.Models;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Queries;

public class MeQuery : IRequest<MeResponse>
{
    public required SessionToken Token { get; init; }
}

public record MeResponse
{
    public required Guid UserId { get; init; }  
    public required string Username { get; init; }
}

public class MeQueryHandler(
    FennecDbContext dbContext    
) : IRequestHandler<MeQuery, MeResponse>
{
    public async Task<MeResponse> Handle(MeQuery request, CancellationToken cancellationToken)
    {
        var userInfo = await dbContext.Set<Session>()
            .Where(x => x.Token == request.Token.Value)
            .Select(x => new
            {
                x.User.Id,
                x.User.Name,
            })
            .SingleOrDefaultAsync(cancellationToken);
        
        if (userInfo == null)
        {
            throw new HttpUnauthorizedException("Invalid token");
        }
        
        return new MeResponse
        {
            UserId = userInfo.Id,
            Username = userInfo.Name,
        };
    }
}