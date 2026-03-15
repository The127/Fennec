using Fennec.App.Embeds;
using Fennec.App.Embeds.Providers;

namespace Fennec.App.Tests.Embeds;

public class YouTubeEmbedProviderTests
{
    private readonly YouTubeEmbedProvider _provider = new();

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    public void CanHandle_YouTubeUrls_ReturnsTrue(string url)
    {
        Assert.True(_provider.CanHandle(new Uri(url)));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("https://youtube.com")]
    [InlineData("https://youtube.com/channel/UC123")]
    [InlineData("https://notyoutube.com/watch?v=dQw4w9WgXcQ")]
    public void CanHandle_NonYouTubeUrls_ReturnsFalse(string url)
    {
        Assert.False(_provider.CanHandle(new Uri(url)));
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/abc123def45", "abc123def45")]
    [InlineData("https://www.youtube.com/embed/abc123def45", "abc123def45")]
    [InlineData("https://www.youtube.com/shorts/abc123def45", "abc123def45")]
    public void CreateEmbed_ExtractsVideoId(string url, string expectedId)
    {
        var uri = new Uri(url);
        var embed = _provider.CreateEmbed(uri);
        var yt = Assert.IsType<YouTubeEmbed>(embed);
        Assert.Equal(expectedId, yt.VideoId);
        Assert.Equal(uri, yt.SourceUrl);
    }

    [Fact]
    public void CreateEmbed_WatchUrlWithExtraParams_ExtractsVideoId()
    {
        var uri = new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=42s");
        var embed = _provider.CreateEmbed(uri);
        var yt = Assert.IsType<YouTubeEmbed>(embed);
        Assert.Equal("dQw4w9WgXcQ", yt.VideoId);
    }
}
