using Fennec.Client;

namespace Fennec.App.Tests.Client;

public class ClientFactoryTests
{
    [Fact]
    public void Create_ReturnsClient()
    {
        var factory = new ClientFactory();
        var client = factory.Create();

        Assert.NotNull(client);
    }
}
