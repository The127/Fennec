using Fennec.App.Exceptions;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.ViewModels;
using Fennec.App.Routing;
using Fennec.App.Services;
using Fennec.App.Services.Auth;
using Fennec.App.Shortcuts;
using Fennec.Client;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShadUI;

namespace Fennec.App.Tests.ViewModels;

public class AppShellViewModelTests
{
    private readonly IAuthStore _authStore = Substitute.For<IAuthStore>();
    private readonly WeakReferenceMessenger _messenger = new();

    private AppShellViewModel CreateShell()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_authStore);
        services.AddSingleton(Substitute.For<IRouter>());
        services.AddSingleton<IMessenger>(_messenger);
        services.AddSingleton(Substitute.For<IAuthService>());
        services.AddSingleton(Substitute.For<IClientFactory>());
        services.AddSingleton(Substitute.For<IServerStore>());
        services.AddSingleton<IExceptionHandler>(NullExceptionHandler.Instance);
        services.AddSingleton<IKeymapService, KeymapService>();
        services.AddSingleton(new ToastManager());
        services.AddSingleton(new DialogManager());
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        return new AppShellViewModel(sp, _authStore, new ToastManager(), new DialogManager(), _messenger);
    }

    private static AuthSession CreateSession(string username = "alice", string url = "fennec.chat") =>
        new()
        {
            Username = username,
            Url = url,
            SessionToken = "token",
            UserId = Guid.NewGuid(),
        };

    [Fact]
    public async Task Starts_logged_in_when_a_session_exists()
    {
        _authStore.GetCurrentAuthSessionAsync().Returns(CreateSession());
        var shell = CreateShell();

        await shell.InitializeAsync();

        Assert.True(shell.IsLoggedIn);
    }

    [Fact]
    public async Task Starts_logged_out_when_no_session_exists()
    {
        _authStore.GetCurrentAuthSessionAsync().Returns((AuthSession?)null);
        var shell = CreateShell();

        await shell.InitializeAsync();

        Assert.True(shell.IsLoggedOut);
    }

    [Fact]
    public void Logs_in_after_login_succeeds()
    {
        var shell = CreateShell();

        _messenger.Send(new LoginSucceededMessage(CreateSession()));

        Assert.True(shell.IsLoggedIn);
    }

    [Fact]
    public void Logs_out_when_user_logged_out()
    {
        var shell = CreateShell();
        _messenger.Send(new LoginSucceededMessage(CreateSession()));

        _messenger.Send(new UserLoggedOutMessage());

        Assert.True(shell.IsLoggedOut);
    }
}
