using System.Security.Cryptography;

namespace Fennec.Api.Services;

public class RandomSessionTokenGenerator : ISessionTokenGenerator
{
    public string GenerateSessionToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
