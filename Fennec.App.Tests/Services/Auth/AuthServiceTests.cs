using Fennec.App.Exceptions;
using Fennec.App.Services.Auth;
using Fennec.App.Services.Storage;
using Fennec.Client;
using Fennec.Shared.Dtos.Auth;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Fennec.App.Tests.Services.Auth;

public class AuthServiceTests
{
    private readonly IAuthStore _authStore = Substitute.For<IAuthStore>();
    private readonly IClientFactory _clientFactory = Substitute.For<IClientFactory>();
    private readonly IFennecClient _client = Substitute.For<IFennecClient>();

    private readonly IDbPathProvider _dbPathProvider = Substitute.For<IDbPathProvider>();

    private AuthService CreateService() => new(_authStore, _clientFactory, NullExceptionHandler.Instance, _dbPathProvider);

    private AuthSession CreateSession() => new()
    {
        Username = "alice",
        Url = "fennec.chat",
        SessionToken = "token",
        UserId = Guid.NewGuid(),
    };

    [Fact]
    public async Task LoginAsync_stores_full_url_in_session()
    {
        var userId = Guid.NewGuid();
        _clientFactory.Create().Returns(_client);
        _client.Auth.LoginAsync(Arg.Any<string>(), Arg.Any<LoginRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new LoginResponseDto { SessionToken = "tok", UserId = userId });

        var service = CreateService();

        await service.LoginAsync("kris", "pass", "https://localhost:7014", CancellationToken.None);

        await _authStore.Received(1).SaveSessionAsync(
            Arg.Is<AuthSession>(s => s.Url == "https://localhost:7014"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_removes_session_even_when_api_call_fails()
    {
        var session = CreateSession();
        _authStore.GetCurrentAuthSessionAsync(Arg.Any<CancellationToken>()).Returns(session);
        _clientFactory.Create().Returns(_client);
        _client.Auth.LogoutAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync<HttpRequestException>();

        var service = CreateService();

        await service.LogoutAsync(CancellationToken.None);

        await _authStore.Received(1).RemoveSessionAsync(session, Arg.Any<CancellationToken>());
    }
}
