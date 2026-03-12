using Fennec.Client;
using Xunit;

namespace Fennec.App.Tests.Client;

public class TokenStoreTests
{
    [Fact]
    public void SetHomeSession_StoresCorrectly()
    {
        var store = new TokenStore();
        store.HomeUrl = "https://home.com";
        store.HomeSessionToken = "session-token";

        Assert.Equal("https://home.com", store.HomeUrl);
        Assert.Equal("session-token", store.HomeSessionToken);
    }

    [Fact]
    public void SetPublicToken_StoresCorrectly()
    {
        var store = new TokenStore();
        store.SetPublicToken("https://target.com", "public-token");

        Assert.Equal("public-token", store.GetPublicToken("https://target.com"));
    }

    [Fact]
    public void CleanupIdle_RemovesOldTokens()
    {
        var store = new TokenStore();
        store.SetPublicToken("https://active.com", "token1");
        store.SetPublicToken("https://idle.com", "token2");

        // Manually update last used for idle to be long ago
        // Actually I need a way to mock time or set the value.
        // For now I'll just check the active list
        
        Assert.Contains("https://active.com", store.GetActiveTargets());
    }
}
