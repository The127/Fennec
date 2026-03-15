using System.Text.RegularExpressions;

namespace Fennec.App.Embeds.Providers;

public partial class ImageEmbedProvider : IEmbedProvider
{
    [GeneratedRegex(@"\.(png|jpe?g|gif|webp)(\?.*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ImageExtensionRegex();

    public bool CanHandle(Uri url)
    {
        return ImageExtensionRegex().IsMatch(url.AbsolutePath);
    }

    public EmbedInfo CreateEmbed(Uri url)
    {
        return new ImageEmbed(url);
    }
}
