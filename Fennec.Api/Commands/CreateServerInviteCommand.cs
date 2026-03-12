using System.Security.Cryptography;
using Fennec.Api.Models;
using Fennec.Api.Security;
using HttpExceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Fennec.Api.Commands;

public record CreateServerInviteCommand : IRequest<CreateServerInviteResponse>
{
    public required Guid ServerId { get; init; }
    public required IAuthPrincipal AuthPrincipal { get; init; }
    public Instant? ExpiresAt { get; init; }
    public int? MaxUses { get; init; }
}

public record CreateServerInviteResponse
{
    public required Guid InviteId { get; init; }
    public required string Code { get; init; }
}

public class CreateServerInviteCommandHandler(
    FennecDbContext dbContext
) : IRequestHandler<CreateServerInviteCommand, CreateServerInviteResponse>
{
    private const string AlphanumericChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public async Task<CreateServerInviteResponse> Handle(CreateServerInviteCommand request, CancellationToken cancellationToken)
    {
        var isMember = await dbContext.Set<ServerMember>()
            .AnyAsync(m => m.ServerId == request.ServerId && m.UserId == request.AuthPrincipal.Id, cancellationToken);

        if (!isMember)
        {
            throw new HttpForbiddenException("You must be a member of the server to create an invite");
        }

        var code = GenerateCode(8);

        var invite = new ServerInvite
        {
            ServerId = request.ServerId,
            Code = code,
            CreatedByUserId = request.AuthPrincipal.Id,
            ExpiresAt = request.ExpiresAt,
            MaxUses = request.MaxUses,
        };

        dbContext.Add(invite);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateServerInviteResponse
        {
            InviteId = invite.Id,
            Code = invite.Code,
        };
    }

    private static string GenerateCode(int length)
    {
        return string.Create(length, (object?)null, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = AlphanumericChars[RandomNumberGenerator.GetInt32(AlphanumericChars.Length)];
            }
        });
    }
}
