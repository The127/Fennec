using Fennec.App.Embeds;
using Fennec.App.Embeds.Providers;

namespace Fennec.App.Tests.Embeds;

public class SpotifyEmbedProviderTests
{
    private readonly SpotifyEmbedProvider _provider = new();

    [Theory]
    [InlineData("https://open.spotify.com/track/6rqhFgbbKwnb9MLmUQDhG6")]
    [InlineData("https://open.spotify.com/album/4aawyAB9vmqN3uQ7FjRGTy")]
    [InlineData("https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("https://open.spotify.com/episode/512ojhOo1ktJprKbVcKN2")]
    public void CanHandle_SpotifyUrls_ReturnsTrue(string url)
    {
        Assert.True(_provider.CanHandle(new Uri(url)));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("https://open.spotify.com")]
    [InlineData("https://open.spotify.com/user/12345")]
    [InlineData("https://spotify.com/track/123")]
    public void CanHandle_NonSpotifyUrls_ReturnsFalse(string url)
    {
        Assert.False(_provider.CanHandle(new Uri(url)));
    }

    [Theory]
    [InlineData("https://open.spotify.com/track/6rqhFgbbKwnb9MLmUQDhG6", "track", "6rqhFgbbKwnb9MLmUQDhG6")]
    [InlineData("https://open.spotify.com/album/4aawyAB9vmqN3uQ7FjRGTy", "album", "4aawyAB9vmqN3uQ7FjRGTy")]
    [InlineData("https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M", "playlist", "37i9dQZF1DXcBWIGoYBM5M")]
    public void CreateEmbed_ExtractsResourceTypeAndId(string url, string expectedType, string expectedId)
    {
        var uri = new Uri(url);
        var embed = _provider.CreateEmbed(uri);
        var sp = Assert.IsType<SpotifyEmbed>(embed);
        Assert.Equal(expectedType, sp.ResourceType);
        Assert.Equal(expectedId, sp.ResourceId);
        Assert.Equal(uri, sp.SourceUrl);
    }
}
