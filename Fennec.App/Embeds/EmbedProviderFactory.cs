namespace Fennec.App.Embeds;

public class EmbedProviderFactory
{
    private readonly IEnumerable<IEmbedProvider> _providers;

    public EmbedProviderFactory(IEnumerable<IEmbedProvider> providers)
    {
        _providers = providers;
    }

    public EmbedInfo? TryCreateEmbed(Uri url)
    {
        foreach (var provider in _providers)
        {
            if (provider.CanHandle(url))
                return provider.CreateEmbed(url);
        }

        return null;
    }
}
