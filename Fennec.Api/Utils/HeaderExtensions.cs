using Fennec.Shared.Models;
using HttpExceptions;
using OneOf;

namespace Fennec.Api.Utils;

public class AuthorizationHeader : OneOfBase<BearerToken, SessionToken>
{
    private AuthorizationHeader(OneOf<BearerToken, SessionToken> input) : base(input)
    {
    }

    public static implicit operator AuthorizationHeader(BearerToken token) => new(token);
    public static implicit operator AuthorizationHeader(SessionToken token) => new(token);
}

public static class HeaderExtensions
{
    extension(IHeaderDictionary headers)
    {
        public void AppendAuthorizationHeader(AuthorizationHeader header)
        {
            var authorizationHeader = header.Match(
                bearer => $"Bearer {bearer}",
                session => $"Session {session}"
            );
        
            headers.Append("Authorization", authorizationHeader);
        }

        public AuthorizationHeader GetAuthorizationHeader()
        {
            var authorizationHeader = headers.Authorization.FirstOrDefault();
        
            if (string.IsNullOrEmpty(authorizationHeader))
            {
                throw new HttpUnauthorizedException("Authorization header is missing");
            }
        
            var parts = authorizationHeader.Split(' ');
            return parts switch
            {
                ["Bearer", var token] => new BearerToken(token.Trim()),
                ["Session", var token] => new SessionToken(token.Trim()),
                _ => throw new HttpBadRequestException("Invalid authorization header format"),
            };
        }
    }
}
