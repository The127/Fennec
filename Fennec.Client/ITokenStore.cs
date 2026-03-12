namespace Fennec.Client;

public interface ITokenStore
{
    string? HomeUrl { get; set; }
    string? HomeSessionToken { get; set; }
    
    string? GetPublicToken(string targetUrl);
    void SetPublicToken(string targetUrl, string token);
    
    void UpdateLastUsed(string targetUrl);
    IEnumerable<string> GetActiveTargets();
    void RemoveTarget(string targetUrl);
}
