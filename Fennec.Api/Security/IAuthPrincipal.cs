namespace Fennec.Api.Security;

public interface IAuthPrincipal
{
    public Guid Id { get; }
    public string Name { get; }
    public string Issuer { get; }
}