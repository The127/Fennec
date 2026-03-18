using Fennec.App.Domain;
using Fennec.App.Exceptions;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.ViewModels;
using Fennec.App.Routing;
using Fennec.App.Services;
using Fennec.App.Services.Auth;
using Fennec.App.Services.Storage;
using Fennec.App.Shortcuts;
using Fennec.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShadUI;

namespace Fennec.App.Tests.ViewModels;

public class AppShellViewModelTests
{
    private readonly IAuthStore _authStore = Substitute.For<IAuthStore>();
    private readonly WeakReferenceMessenger _messenger = new();

    private AppShellViewModel CreateShell(IUpdateService? updateService = null)
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
        var settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.LoadAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings());
        services.AddSingleton(settingsStore);
        services.AddSingleton(new ToastManager());
        services.AddSingleton(new DialogManager());
        services.AddSingleton(Substitute.For<IChannelSubscriptionService>());
        services.AddSingleton(Substitute.For<IVoiceCallService>());
        services.AddSingleton(Substitute.For<IVoicePresenceService>());
        services.AddDbContextFactory<AppDbContext>(opts => opts.UseSqlite("DataSource=:memory:"));
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        return new AppShellViewModel(sp, _authStore, Substitute.For<IDbPathProvider>(), new ToastManager(), new DialogManager(), _messenger, updateService ?? Substitute.For<IUpdateService>());
    }

    private static AuthSession CreateSession(string username = "alice", string url = "fennec.chat") =>
        new()
        {
            Username = username,
            Url = new InstanceUrl(url),
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

    [Fact]
    public async Task Clears_loading_status_when_no_update_found()
    {
        var updateService = Substitute.For<IUpdateService>();
        updateService.CheckForUpdateAsync().Returns((UpdateInfo?)null);
        _authStore.GetCurrentAuthSessionAsync().Returns((AuthSession?)null);

        var shell = CreateShell(updateService);
        var loadingVm = Assert.IsType<LoadingViewModel>(shell.CurrentViewModel);

        await shell.InitializeAsync();

        Assert.Equal(string.Empty, loadingVm.Status);
        Assert.False(loadingVm.IsUpdating);
    }

    [Fact]
    public async Task Attempts_download_when_update_found()
    {
        var update = new UpdateInfo("1.2.3", "https://example.com/fennec");
        var updateService = Substitute.For<IUpdateService>();
        updateService.CheckForUpdateAsync().Returns(update);
        updateService
            .DownloadAndApplyAsync(Arg.Any<UpdateInfo>(), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new HttpRequestException("network error")));
        _authStore.GetCurrentAuthSessionAsync().Returns((AuthSession?)null);

        var shell = CreateShell(updateService);

        await shell.InitializeAsync();

        await updateService.Received(1)
            .DownloadAndApplyAsync(update, Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Falls_back_to_banner_when_download_fails()
    {
        var update = new UpdateInfo("1.2.3", "https://example.com/fennec");
        var updateService = Substitute.For<IUpdateService>();
        updateService.CheckForUpdateAsync().Returns(update);
        updateService
            .DownloadAndApplyAsync(Arg.Any<UpdateInfo>(), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new HttpRequestException("network error")));
        _authStore.GetCurrentAuthSessionAsync().Returns((AuthSession?)null);

        var shell = CreateShell(updateService);
        var loadingVm = Assert.IsType<LoadingViewModel>(shell.CurrentViewModel);

        await shell.InitializeAsync();

        Assert.Equal(update, shell.AvailableUpdate);
        Assert.True(shell.IsUpdateAvailable);
        Assert.False(loadingVm.IsUpdating);
    }
}
