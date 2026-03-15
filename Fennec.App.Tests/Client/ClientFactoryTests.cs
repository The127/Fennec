using Fennec.Client;

namespace Fennec.App.Tests.Client;

public class ClientFactoryTests
{
    [Fact]
    public void Create_ReturnsClient()
    {
        var factory = new ClientFactory(new TokenStore());
        var client = factory.Create();

        Assert.NotNull(client);
    }
}
