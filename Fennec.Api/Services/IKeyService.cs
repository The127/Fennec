using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Fennec.Api.Middlewares;
using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Api.Settings;
using Fennec.Api.Utils;
using Fennec.Shared.Dtos.WellKnown;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NodaTime;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace Fennec.Api.Services;

public interface IKeyService
{
    public string GetSignedToken(User user);
    public Task<IAuthPrincipal> VerifyTokenAsync(BearerToken jwt, CancellationToken cancellationToken);

    public string PublicKeyPem { get; }
}

public class KeyService : IKeyService
{
    private readonly IOptions<FennecSettings> _fennecSettings;
    private readonly IClockService _clockService;

    private readonly SigningCredentials _signingCredentials;

    public string PublicKeyPem { get; }

    private ConcurrentDictionary<string, RsaSecurityKey> _publicKeys = new();
    private SemaphoreSlim _publicKeyFetchLock = new(1);

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
        PublicKeyPem = rsa.ExportRSAPublicKeyPem();
    }

    public async Task<IAuthPrincipal> VerifyTokenAsync(BearerToken bearerToken, CancellationToken cancellationToken)
    {
        var tokenHandler = new JsonWebTokenHandler();
        var jwt = tokenHandler.ReadJsonWebToken(bearerToken.Value);

        var instancePublicKey = await FetchInstancePublicKey(jwt.Issuer, cancellationToken);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,

            ValidateAudience = true,
            ValidAudiences = ["fennec-client"],

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = instancePublicKey,

            RequireSignedTokens = true,
        };

        try
        {
            await tokenHandler.ValidateTokenAsync(bearerToken.Value, validationParameters);
        }
        catch (SecurityTokenMalformedException)
        {
            throw new BadHttpRequestException("Invalid token format");
        }

        try
        {
            return new AuthenticationMiddleware.AuthenticationModel
            {
                Id = Guid.Parse(jwt.Subject),
                IsLocal = jwt.Issuer == _fennecSettings.Value.IssuerUrl,
            };
        }
        catch (FormatException)
        {
            throw new BadHttpRequestException("Invalid token format");
        }
    }

    private async Task<RsaSecurityKey> FetchInstancePublicKey(string issuer, CancellationToken cancellationToken)
    {
        if (_publicKeys.TryGetValue(issuer, out var key))
        {
            return key;
        }

        try
        {
            await _publicKeyFetchLock.WaitAsync(cancellationToken);

            if (_publicKeys.TryGetValue(issuer, out key))
            {
                return key;
            }
            
            var uriBuilder = new UriBuilder(issuer)
            {
                Path = ".well-known/fennec/public-key",
            };

            if (uriBuilder.Scheme != "https")
            {
                throw new BadHttpRequestException("Issuer has to use https scheme");
            }

            var publicKeyResponse =
                await new HttpClient().GetFromJsonAsync<GetPublicKeyResponseDto>(uriBuilder.Uri, cancellationToken);
            if (publicKeyResponse is null)
            {
                throw new BadHttpRequestException("Issuer has to return a valid public key");
            }

            var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyResponse.PublicKeyPem);
            
            key = new RsaSecurityKey(rsa);
            
            _publicKeys.AddOrUpdate(issuer, key, (_, _) => key);

            return key;
        }
        finally
        {
            _publicKeyFetchLock.Release();
        }
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