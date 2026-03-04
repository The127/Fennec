using System.Text.Json;
using Fennec.Api.Models;
using MediatR;

namespace Fennec.Api.Commands;

public record RegisterUserCommand : IRequest
{
    public required string Name { get; set; }
    public required string Password { get; set; }
}

public class RegisterUserCommandHandler(
    FennecDbContext dbContext    
) : IRequestHandler<RegisterUserCommand>
{
    public async Task Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var user = new User
        {
            Name = request.Name,
            IsLocal = true,
        };

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var password = new AuthMethod
        {
            UserId = user.Id,
            Type = AuthMethodType.Password,
            Details = JsonSerializer.SerializeToDocument(new PasswordAuthMethodDetails
            {
                Hash = hash,
            })
        };
        
        dbContext.AddRange(user, password);
        
        await dbContext.SaveChangesAsync( cancellationToken);
    }
}