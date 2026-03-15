using System.Text.RegularExpressions;

namespace Fennec.App.Embeds.Providers;

public partial class TwitchEmbedProvider : IEmbedProvider
{
    // twitch.tv/channel, twitch.tv/videos/ID, twitch.tv/channel/clip/SLUG, clips.twitch.tv/SLUG
    [GeneratedRegex(@"(?:twitch\.tv/videos/(\d+)|clips\.twitch\.tv/([a-zA-Z0-9_-]+)|twitch\.tv/([a-zA-Z0-9_]+)/clip/([a-zA-Z0-9_-]+)|twitch\.tv/([a-zA-Z0-9_]+))", RegexOptions.Compiled)]
    private static partial Regex TwitchRegex();

    public bool CanHandle(Uri url)
    {
        var host = url.Host;
        return host is "twitch.tv" or "www.twitch.tv" or "clips.twitch.tv" or "m.twitch.tv"
               && TwitchRegex().IsMatch(url.AbsoluteUri);
    }

    public EmbedInfo CreateEmbed(Uri url)
    {
        var match = TwitchRegex().Match(url.AbsoluteUri);

        if (match.Groups[1].Success)
            return new TwitchEmbed(url, TwitchContentType.Video, match.Groups[1].Value);

        if (match.Groups[2].Success)
            return new TwitchEmbed(url, TwitchContentType.Clip, match.Groups[2].Value);

        if (match.Groups[4].Success)
            return new TwitchEmbed(url, TwitchContentType.Clip, match.Groups[4].Value);

        return new TwitchEmbed(url, TwitchContentType.Channel, match.Groups[5].Value);
    }
}
