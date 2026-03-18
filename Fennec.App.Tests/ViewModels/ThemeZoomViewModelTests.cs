using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services;
using Fennec.App.Themes;
using Fennec.App.ViewModels;
using NSubstitute;
using ShadUI;

namespace Fennec.App.Tests.ViewModels;

public class ThemeZoomViewModelTests
{
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly ToastManager _toastManager = new();

    public ThemeZoomViewModelTests()
    {
        _settingsStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings());
    }

    private ThemeZoomViewModel CreateViewModel() =>
        new(_settingsStore, _messenger, _toastManager);

    private void CaptureOnSave(out Func<AppSettings?> getSaved)
    {
        AppSettings? saved = null;
        _settingsStore.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>())
            .Returns(c => { saved = c.Arg<AppSettings>(); return Task.CompletedTask; });
        getSaved = () => saved;
    }

    [Fact]
    public async Task startup_applies_the_saved_theme_mode()
    {
        _settingsStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings { ThemeMode = AppThemes.Dark });
        var vm = CreateViewModel();

        await vm.InitializeAsync();

        Assert.Equal(AppThemes.Dark, vm.CurrentThemeMode);
    }

    [Fact]
    public async Task startup_broadcasts_the_saved_zoom_level()
    {
        _settingsStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings { ZoomLevel = 1.5 });
        ZoomChangedMessage? received = null;
        _messenger.Register<ZoomChangedMessage>(this, (_, m) => received = m);
        var vm = CreateViewModel();

        await vm.InitializeAsync();

        Assert.Equal(1.5, received?.ZoomLevel);
    }

    [AvaloniaFact]
    public async Task zooming_in_increases_the_zoom_level_by_one_step()
    {
        _settingsStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings { ZoomLevel = 1.0 });
        CaptureOnSave(out var getSaved);
        var vm = CreateViewModel();

        await vm.ZoomInCommand.ExecuteAsync(null);

        Assert.Equal(1.1, getSaved()!.ZoomLevel, precision: 1);
    }

    [AvaloniaFact]
    public async Task zooming_out_decreases_the_zoom_level_by_one_step()
    {
        _settingsStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings { ZoomLevel = 1.0 });
        CaptureOnSave(out var getSaved);
        var vm = CreateViewModel();

        await vm.ZoomOutCommand.ExecuteAsync(null);

        Assert.Equal(0.9, getSaved()!.ZoomLevel, precision: 1);
    }

    [AvaloniaFact]
    public async Task resetting_zoom_restores_the_default_level()
    {
        _settingsStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings { ZoomLevel = 1.5 });
        CaptureOnSave(out var getSaved);
        var vm = CreateViewModel();

        await vm.ZoomResetCommand.ExecuteAsync(null);

        Assert.Equal(1.0, getSaved()!.ZoomLevel);
    }

    [AvaloniaFact]
    public async Task zoom_level_cannot_exceed_the_maximum()
    {
        _settingsStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings { ZoomLevel = 2.0 });
        CaptureOnSave(out var getSaved);
        var vm = CreateViewModel();

        await vm.ZoomInCommand.ExecuteAsync(null);

        Assert.Equal(2.0, getSaved()!.ZoomLevel);
    }

    [AvaloniaFact]
    public async Task zoom_level_cannot_go_below_the_minimum()
    {
        _settingsStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings { ZoomLevel = 0.5 });
        CaptureOnSave(out var getSaved);
        var vm = CreateViewModel();

        await vm.ZoomOutCommand.ExecuteAsync(null);

        Assert.Equal(0.5, getSaved()!.ZoomLevel);
    }

    [AvaloniaFact]
    public async Task zooming_in_broadcasts_the_new_zoom_level()
    {
        _settingsStore.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings { ZoomLevel = 1.0 });
        ZoomChangedMessage? received = null;
        _messenger.Register<ZoomChangedMessage>(this, (_, m) => received = m);
        var vm = CreateViewModel();

        await vm.ZoomInCommand.ExecuteAsync(null);

        Assert.NotNull(received);
        Assert.Equal(1.1, received!.ZoomLevel, precision: 1);
    }

    [Fact]
    public void only_one_theme_mode_is_active_at_a_time()
    {
        var vm = CreateViewModel();

        vm.CurrentThemeMode = AppThemes.Light;
        Assert.True(vm.IsLightMode);
        Assert.False(vm.IsAutoMode);
        Assert.False(vm.IsDarkMode);

        vm.CurrentThemeMode = AppThemes.Auto;
        Assert.False(vm.IsLightMode);
        Assert.True(vm.IsAutoMode);
        Assert.False(vm.IsDarkMode);

        vm.CurrentThemeMode = AppThemes.Dark;
        Assert.False(vm.IsLightMode);
        Assert.False(vm.IsAutoMode);
        Assert.True(vm.IsDarkMode);
    }
}
