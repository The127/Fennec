namespace Fennec.App.Embeds;

public abstract record EmbedInfo(Uri SourceUrl);

public record YouTubeEmbed(Uri SourceUrl, string VideoId) : EmbedInfo(SourceUrl);

public record SpotifyEmbed(Uri SourceUrl, string ResourceType, string ResourceId) : EmbedInfo(SourceUrl);

public record ImageEmbed(Uri SourceUrl) : EmbedInfo(SourceUrl);

public enum TwitchContentType { Channel, Video, Clip }

public record TwitchEmbed(Uri SourceUrl, TwitchContentType ContentType, string ContentId) : EmbedInfo(SourceUrl);
