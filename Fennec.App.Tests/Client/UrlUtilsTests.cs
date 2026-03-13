using Fennec.Client;

namespace Fennec.App.Tests.Client;

public class UrlUtilsTests
{
    [Theory]
    [InlineData("localhost:7014", "https://localhost:7014")]
    [InlineData("https://localhost:7014", "https://localhost:7014")]
    [InlineData("http://localhost:7014", "http://localhost:7014")]
    [InlineData("fennec.social", "https://fennec.social")]
    public void NormalizeBaseUrl_ShouldPrependHttpsIfMissing(string input, string expected)
    {
        var result = UrlUtils.NormalizeBaseUrl(input);
        Assert.Equal(expected, result);
    }
}
