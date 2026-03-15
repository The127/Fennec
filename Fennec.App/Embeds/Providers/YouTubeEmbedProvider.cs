using System.Text.RegularExpressions;

namespace Fennec.App.Embeds.Providers;

public partial class YouTubeEmbedProvider : IEmbedProvider
{
    // youtube.com/watch?v=ID, youtu.be/ID, youtube.com/embed/ID, youtube.com/shorts/ID
    [GeneratedRegex(@"(?:youtube\.com/(?:watch\?.*v=|embed/|shorts/)|youtu\.be/)([a-zA-Z0-9_-]{11})", RegexOptions.Compiled)]
    private static partial Regex YouTubeRegex();

    public bool CanHandle(Uri url)
    {
        var host = url.Host;
        return (host is "youtube.com" or "www.youtube.com" or "youtu.be" or "m.youtube.com")
               && YouTubeRegex().IsMatch(url.AbsoluteUri);
    }

    public EmbedInfo CreateEmbed(Uri url)
    {
        var match = YouTubeRegex().Match(url.AbsoluteUri);
        return new YouTubeEmbed(url, match.Groups[1].Value);
    }
}
