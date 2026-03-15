using Fennec.App.Embeds;
using Fennec.App.Embeds.Providers;

namespace Fennec.App.Tests.Embeds;

public class TwitchEmbedProviderTests
{
    private readonly TwitchEmbedProvider _provider = new();

    [Theory]
    [InlineData("https://www.twitch.tv/shroud")]
    [InlineData("https://twitch.tv/shroud")]
    [InlineData("https://m.twitch.tv/shroud")]
    [InlineData("https://www.twitch.tv/videos/123456789")]
    [InlineData("https://clips.twitch.tv/AwesomeClipSlug-abc123")]
    [InlineData("https://www.twitch.tv/shroud/clip/AwesomeClipSlug-abc123")]
    public void CanHandle_TwitchUrls_ReturnsTrue(string url)
    {
        Assert.True(_provider.CanHandle(new Uri(url)));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("https://nottwitch.tv/shroud")]
    [InlineData("https://twitch.com/shroud")]
    public void CanHandle_NonTwitchUrls_ReturnsFalse(string url)
    {
        Assert.False(_provider.CanHandle(new Uri(url)));
    }

    [Theory]
    [InlineData("https://www.twitch.tv/shroud", TwitchContentType.Channel, "shroud")]
    [InlineData("https://twitch.tv/ninja", TwitchContentType.Channel, "ninja")]
    public void CreateEmbed_Channel_ExtractsCorrectly(string url, TwitchContentType expectedType, string expectedId)
    {
        var uri = new Uri(url);
        var embed = _provider.CreateEmbed(uri);
        var tw = Assert.IsType<TwitchEmbed>(embed);
        Assert.Equal(expectedType, tw.ContentType);
        Assert.Equal(expectedId, tw.ContentId);
        Assert.Equal(uri, tw.SourceUrl);
    }

    [Fact]
    public void CreateEmbed_Video_ExtractsVideoId()
    {
        var uri = new Uri("https://www.twitch.tv/videos/123456789");
        var embed = _provider.CreateEmbed(uri);
        var tw = Assert.IsType<TwitchEmbed>(embed);
        Assert.Equal(TwitchContentType.Video, tw.ContentType);
        Assert.Equal("123456789", tw.ContentId);
    }

    [Fact]
    public void CreateEmbed_ClipsDomain_ExtractsSlug()
    {
        var uri = new Uri("https://clips.twitch.tv/AwesomeClipSlug-abc123");
        var embed = _provider.CreateEmbed(uri);
        var tw = Assert.IsType<TwitchEmbed>(embed);
        Assert.Equal(TwitchContentType.Clip, tw.ContentType);
        Assert.Equal("AwesomeClipSlug-abc123", tw.ContentId);
    }

    [Fact]
    public void CreateEmbed_ChannelClipPath_ExtractsSlug()
    {
        var uri = new Uri("https://www.twitch.tv/shroud/clip/AwesomeClipSlug-abc123");
        var embed = _provider.CreateEmbed(uri);
        var tw = Assert.IsType<TwitchEmbed>(embed);
        Assert.Equal(TwitchContentType.Clip, tw.ContentType);
        Assert.Equal("AwesomeClipSlug-abc123", tw.ContentId);
    }
}
