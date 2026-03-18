namespace Fennec.App.Domain;

public record InstanceUrl
{
    private readonly Uri _uri;

    public InstanceUrl(string url)
    {
        if (!url.Contains("://"))
            url = $"https://{url}";

        _uri = new Uri(url.TrimEnd('/'));
    }

    public string Host => _uri.Host;

    public override string ToString() =>
        _uri.IsDefaultPort ? _uri.Host : $"{_uri.Host}:{_uri.Port}";

    public static InstanceUrl? From(string? url) => url is null ? null : new(url);

    public static implicit operator string(InstanceUrl u) => u.ToString();
}
