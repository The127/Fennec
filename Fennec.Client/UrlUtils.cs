namespace Fennec.Client;

internal static class UrlUtils
{
    public static string NormalizeBaseUrl(string baseUrl)
    {
        if (baseUrl.Contains("://"))
        {
            return baseUrl.TrimEnd('/');
        }
        
        return $"https://{baseUrl}".TrimEnd('/');
    }
}
