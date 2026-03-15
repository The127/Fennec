namespace Fennec.App.Embeds;

public interface IEmbedProvider
{
    bool CanHandle(Uri url);
    EmbedInfo CreateEmbed(Uri url);
}
