using System.Text.RegularExpressions;

namespace Fennec.App.Embeds.Providers;

public partial class SpotifyEmbedProvider : IEmbedProvider
{
    // open.spotify.com/{track|album|playlist|episode}/{id}
    [GeneratedRegex(@"open\.spotify\.com/(track|album|playlist|episode)/([a-zA-Z0-9]+)", RegexOptions.Compiled)]
    private static partial Regex SpotifyRegex();

    public bool CanHandle(Uri url)
    {
        return url.Host is "open.spotify.com" && SpotifyRegex().IsMatch(url.AbsoluteUri);
    }

    public EmbedInfo CreateEmbed(Uri url)
    {
        var match = SpotifyRegex().Match(url.AbsoluteUri);
        return new SpotifyEmbed(url, match.Groups[1].Value, match.Groups[2].Value);
    }
}
