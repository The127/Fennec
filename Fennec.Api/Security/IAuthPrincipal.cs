namespace Fennec.Api.Security;

public interface IAuthPrincipal
{
    public Guid Id { get; }
    public bool IsLocal { get; }
}