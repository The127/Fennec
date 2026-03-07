using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Fennec.Api.Models;
using Fennec.Api.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NodaTime;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace Fennec.Api.Services;

public interface IKeyService
{
    public string GetSignedToken(User user);
    public string PublicKeyPem { get; }
}

public class KeyService : IKeyService
{
    private readonly IOptions<FennecSettings> _fennecSettings;
    private readonly IClockService _clockService;
    
    private readonly SigningCredentials _signingCredentials;
    private readonly string _publicKeyPem;

    public string PublicKeyPem => _publicKeyPem;

    public KeyService(
        IOptions<KeySettings> keyOptions,
        IOptions<FennecSettings> fennecSettings,
        IClockService clockService)
    {
        _fennecSettings = fennecSettings;
        _clockService = clockService;
        
        var pem = File.ReadAllText(keyOptions.Value.PrivateKeyPath);
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        _signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        _publicKeyPem = rsa.ExportRSAPublicKeyPem();
    }
    
    public string GetSignedToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _fennecSettings.Value.IssuerUrl,
            audience: "fennec-client",
            claims: claims,
            expires: (_clockService.GetCurrentInstant() + Duration.FromMinutes(5)).ToDateTimeUtc(),
            signingCredentials: _signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}