using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Fennec.Api.Models;
using Fennec.Api.Security;
using Fennec.Api.Settings;
using Fennec.Shared.Dtos.WellKnown;
using Fennec.Shared.Models;
using HttpExceptions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NodaTime;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace Fennec.Api.Services;

public record AuthenticationModel : IAuthPrincipal
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Issuer { get; init; }
}

public interface IKeyService
{
    public string SignPayload(string payload);

    public Task VerifyPayloadAsync(string payload, string signature, string instanceUrl,
        CancellationToken cancellationToken);

    public string GetSignedToken(User user, string audience);
    public Task<IAuthPrincipal> VerifyTokenAsync(BearerToken jwt, CancellationToken cancellationToken);

    public string PublicKeyPem { get; }
}

public class KeyService : IKeyService
{
    private readonly IOptions<FennecSettings> _fennecSettings;
    private readonly IClockService _clockService;

    private readonly SigningCredentials _signingCredentials;
    private readonly RSA _rsa = RSA.Create();

    public string PublicKeyPem { get; }

    private readonly ConcurrentDictionary<string, RsaSecurityKey> _publicKeys = [];
    private readonly SemaphoreSlim _publicKeyFetchLock = new(1);

    public KeyService(
        IOptions<KeySettings> keyOptions,
        IOptions<FennecSettings> fennecSettings,
        IClockService clockService)
    {
        _fennecSettings = fennecSettings;
        _clockService = clockService;

        var pem = File.ReadAllText(keyOptions.Value.PrivateKeyPath);
        _rsa.ImportFromPem(pem);
        _signingCredentials = new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256);
        PublicKeyPem = _rsa.ExportRSAPublicKeyPem();
    }

    public async Task<IAuthPrincipal> VerifyTokenAsync(BearerToken bearerToken, CancellationToken cancellationToken)
    {
        var tokenHandler = new JsonWebTokenHandler();
        var jwt = tokenHandler.ReadJsonWebToken(bearerToken.Value);

        var instancePublicKey = await FetchInstancePublicKey(jwt.Issuer, cancellationToken);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,

            ValidateAudience = true,
            ValidAudience = _fennecSettings.Value.IssuerUrl,

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
            return new AuthenticationModel
            {
                Id = Guid.Parse(jwt.Subject),
                Name = jwt.TryGetClaim("name", out var name)
                    ? name.Value
                    : throw new BadHttpRequestException("Missing name claim"),
                Issuer = jwt.Issuer,
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

    public string SignPayload(string payload)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = _rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }

    public async Task VerifyPayloadAsync(string payload, string signature, string instanceUrl,
        CancellationToken cancellationToken)
    {
        var instancePublicKey = await FetchInstancePublicKey(instanceUrl, cancellationToken);
        var isValid = instancePublicKey.Rsa.VerifyData(
            Encoding.UTF8.GetBytes(payload),
            Convert.FromBase64String(signature),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        
        if (!isValid)
        {
            throw new HttpUnauthorizedException("Invalid signature");
        }
    }

    public string GetSignedToken(User user, string audience)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, user.Name),
            new Claim("DisplayName", user.DisplayName ?? user.Name),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var expiry = Duration.FromMinutes(5);
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            expiry = Duration.FromDays(1);
        }

        var token = new JwtSecurityToken(
            issuer: _fennecSettings.Value.IssuerUrl,
            audience: audience,
            claims: claims,
            expires: (_clockService.GetCurrentInstant() + expiry).ToDateTimeUtc(),
            signingCredentials: _signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}