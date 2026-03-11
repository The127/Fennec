using Fennec.Client;
using Xunit;

namespace Fennec.App.Tests.Client;

public class ClientFactoryTests
{
    [Theory]
    [InlineData("fennec.chat", "https://fennec.chat/")]
    [InlineData("http://localhost:5176", "http://localhost:5176/")]
    [InlineData("https://localhost:7014", "https://localhost:7014/")]
    public void Create_NormalizesUrl(string inputUrl, string expectedBaseAddress)
    {
        // Arrange
        var factory = new ClientFactory(inputUrl);

        // Act
        var client = factory.Create();

        // Assert
        Assert.Equal(expectedBaseAddress, client.BaseAddress);
    }
}