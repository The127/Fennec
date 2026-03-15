using Fennec.App.Embeds;
using Fennec.App.Embeds.Providers;

namespace Fennec.App.Tests.Embeds;

public class EmbedProviderFactoryTests
{
    [Fact]
    public void TryCreateEmbed_YouTubeUrl_ReturnsYouTubeEmbed()
    {
        var factory = new EmbedProviderFactory([
            new YouTubeEmbedProvider(),
            new SpotifyEmbedProvider(),
            new ImageEmbedProvider(),
        ]);

        var result = factory.TryCreateEmbed(new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
        Assert.IsType<YouTubeEmbed>(result);
    }

    [Fact]
    public void TryCreateEmbed_UnknownUrl_ReturnsNull()
    {
        var factory = new EmbedProviderFactory([
            new YouTubeEmbedProvider(),
            new SpotifyEmbedProvider(),
            new ImageEmbedProvider(),
        ]);

        var result = factory.TryCreateEmbed(new Uri("https://example.com/page"));
        Assert.Null(result);
    }

    [Fact]
    public void TryCreateEmbed_FirstMatchWins()
    {
        // Image provider before others — a YouTube thumbnail URL ending in .jpg
        // should match image provider first if it's registered first
        var factory = new EmbedProviderFactory([
            new ImageEmbedProvider(),
            new YouTubeEmbedProvider(),
        ]);

        var result = factory.TryCreateEmbed(new Uri("https://example.com/photo.jpg"));
        Assert.IsType<ImageEmbed>(result);
    }

    [Fact]
    public void TryCreateEmbed_SpotifyUrl_ReturnsSpotifyEmbed()
    {
        var factory = new EmbedProviderFactory([
            new YouTubeEmbedProvider(),
            new SpotifyEmbedProvider(),
            new ImageEmbedProvider(),
        ]);

        var result = factory.TryCreateEmbed(new Uri("https://open.spotify.com/track/abc123"));
        Assert.IsType<SpotifyEmbed>(result);
    }
}
