using HttpExceptions;
using OneOf;
using ValueOf;

namespace Fennec.Api.Utils;

public class SessionToken : ValueOf<string, SessionToken>;
public class BearerToken : ValueOf<string, BearerToken>;

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
                ["Bearer", _] => BearerToken.From(parts[1].Trim()),
                ["Session", _] => SessionToken.From(parts[1].Trim()),
                _ => throw new HttpBadRequestException("Invalid authorization header format")
            };
        }
    }
}
