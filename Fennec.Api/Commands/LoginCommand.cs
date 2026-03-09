using System.Security.Cryptography;
using Fennec.Api.Models;
using Fennec.Shared.Models;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Commands;

public class LoginCommand : IRequest<LoginResponse>
{
    public required string Name { get; init; }
    public required string Password { get; init; }
}

public class LoginResponse
{
    public required SessionToken Token { get; init; }
}

public class LoginCommandHandler(
    FennecDbContext dbContext
) : IRequestHandler<LoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var passwordAuthMethod = await dbContext.Set<AuthMethod>()
            .Where(x => x.User.Name == request.Name)
            .Where(x => x.Type == AuthMethodType.Password)
            .WherePasswordAuthMethod()
            .SingleOrDefaultAsync(cancellationToken);

        if (passwordAuthMethod is null || !BCrypt.Net.BCrypt.Verify(request.Password, passwordAuthMethod.Details.Hash))
        {
            throw new HttpUnauthorizedException();
        }

        var session = new Session
        {
            UserId = passwordAuthMethod.UserId,
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
        };

        dbContext.Add(session);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResponse
        {
            Token = new SessionToken(session.Token),
        };
    }
}