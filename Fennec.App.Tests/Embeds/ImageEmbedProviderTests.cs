using Fennec.App.Embeds;
using Fennec.App.Embeds.Providers;

namespace Fennec.App.Tests.Embeds;

public class ImageEmbedProviderTests
{
    private readonly ImageEmbedProvider _provider = new();

    [Theory]
    [InlineData("https://example.com/image.png")]
    [InlineData("https://example.com/image.jpg")]
    [InlineData("https://example.com/image.jpeg")]
    [InlineData("https://example.com/image.gif")]
    [InlineData("https://example.com/image.webp")]
    [InlineData("https://example.com/image.PNG")]
    [InlineData("https://example.com/path/to/image.jpg?width=100")]
    public void CanHandle_ImageUrls_ReturnsTrue(string url)
    {
        Assert.True(_provider.CanHandle(new Uri(url)));
    }

    [Theory]
    [InlineData("https://example.com/page")]
    [InlineData("https://example.com/file.pdf")]
    [InlineData("https://example.com/file.mp4")]
    [InlineData("https://example.com/file.svg")]
    public void CanHandle_NonImageUrls_ReturnsFalse(string url)
    {
        Assert.False(_provider.CanHandle(new Uri(url)));
    }

    [Fact]
    public void CreateEmbed_ReturnsImageEmbed()
    {
        var uri = new Uri("https://example.com/photo.png");
        var embed = _provider.CreateEmbed(uri);
        var img = Assert.IsType<ImageEmbed>(embed);
        Assert.Equal(uri, img.SourceUrl);
    }
}
