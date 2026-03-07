using Fennec.Api.Models;

namespace Fennec.Api.Services;

public interface IKeyService
{
    public string GetSignedToken(User user);
}

public class KeyService : IKeyService
{
    public string GetSignedToken(User user)
    {
        return "signed-token";
    }
}