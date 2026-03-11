using Fennec.Api.Models;
using Fennec.Api.Services;
using Fennec.Shared.Models;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public record CreatePublicTokenCommand : IRequest<CreatePublicTokenResponse>
{
    public required SessionToken Token { get; init; }
    public required string Audience { get; init; }
}

public record CreatePublicTokenResponse
{
    public required string Token { get; init; } 
}

public class CreatePublicTokenCommandHandler(
    FennecDbContext dbContext,
    IKeyService keyService
) : IRequestHandler<CreatePublicTokenCommand, CreatePublicTokenResponse>
{
    public async Task<CreatePublicTokenResponse> Handle(CreatePublicTokenCommand request,
        CancellationToken cancellationToken)
    {
        var sessionInfo = await dbContext
            .Set<Session>()
            .Where(x => x.Token == request.Token.Value)
            .Select(x => new { x.User })
            .SingleOrDefaultAsync(cancellationToken);

        if (sessionInfo is null)
        {
            throw new HttpUnauthorizedException("Invalid token");
        }

        return new CreatePublicTokenResponse
        {
            Token = keyService.GetSignedToken(sessionInfo.User, request.Audience),
        };
    }
}