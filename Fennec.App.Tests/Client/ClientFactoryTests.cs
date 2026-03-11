using Fennec.Client;

namespace Fennec.App.Tests.Client;

public class ClientFactoryTests
{
    [Theory]
    [InlineData("fennec.chat", "https://fennec.chat/")]
    [InlineData("localhost:5176", "https://localhost:5176/")]
    public void Create_NormalizesUrl(string inputUrl, string expectedBaseAddress)
    {
        var factory = new ClientFactory();

        var client = factory.Create(inputUrl);

        Assert.Equal(expectedBaseAddress, client.BaseAddress);
    }

    [Theory]
    [InlineData("http://localhost:5176")]
    [InlineData("https://fennec.chat")]
    public void Create_ThrowsOnScheme(string inputUrl)
    {
        var factory = new ClientFactory();

        Assert.Throws<ArgumentException>(() => factory.Create(inputUrl));
    }
}
