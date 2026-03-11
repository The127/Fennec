using Fennec.Client;
using Xunit;

namespace Fennec.App.Tests.Client;

public class ClientFactoryTests
{
    [Theory]
    [InlineData("fennec.chat", "https://fennec.chat/")]
    [InlineData("localhost:5176", "https://localhost:5176/")]
    public void Create_NormalizesUrl(string inputUrl, string expectedBaseAddress)
    {
        // Arrange
        var factory = new ClientFactory(inputUrl);

        // Act
        var client = factory.Create();

        // Assert
        Assert.Equal(expectedBaseAddress, client.BaseAddress);
    }

    [Theory]
    [InlineData("http://localhost:5176")]
    [InlineData("https://fennec.chat")]
    public void Create_ThrowsOnScheme(string inputUrl)
    {
        // Arrange
        var factory = new ClientFactory(inputUrl);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => factory.Create());
    }
}