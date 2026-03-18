using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Domain;
using Fennec.App.Services;

namespace Fennec.App.ViewModels.Settings;

public partial class VideoSettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;
    private bool _initialized;

    public IReadOnlyList<ScreenShareResolution> ResolutionOptions { get; } = ScreenShareResolution.All;
    public List<int> FrameRateOptions { get; } = [15, 30, 60];
    public List<int> ViewerScaleOptions { get; } = [100, 75, 50];

    [ObservableProperty]
    private ScreenShareResolution _selectedResolution;

    [ObservableProperty]
    private int _bitrateKbps;

    [ObservableProperty]
    private int _selectedFrameRate;

    [ObservableProperty]
    private int _selectedViewerScale;

    public VideoSettingsViewModel(ISettingsStore settingsStore, AppSettings currentSettings)
    {
        _settingsStore = settingsStore;

        _selectedResolution = currentSettings.ScreenShareResolution;
        _bitrateKbps = Math.Clamp(currentSettings.ScreenShareBitrateKbps, 500, 50_000);
        _selectedFrameRate = FrameRateOptions.Contains(currentSettings.ScreenShareFrameRate)
            ? currentSettings.ScreenShareFrameRate
            : 30;
        _selectedViewerScale = ViewerScaleOptions.Contains(currentSettings.ViewerDownscalePercent)
            ? currentSettings.ViewerDownscalePercent
            : 100;

        _initialized = true;
    }

    partial void OnSelectedResolutionChanged(ScreenShareResolution value)
    {
        if (_initialized) _ = SaveAsync();
    }

    partial void OnBitrateKbpsChanged(int value)
    {
        if (_initialized) _ = SaveAsync();
    }

    partial void OnSelectedFrameRateChanged(int value)
    {
        if (_initialized) _ = SaveAsync();
    }

    partial void OnSelectedViewerScaleChanged(int value)
    {
        if (_initialized) _ = SaveAsync();
    }

    private async Task SaveAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        settings.ScreenShareResolution = SelectedResolution;
        settings.ScreenShareBitrateKbps = BitrateKbps;
        settings.ScreenShareFrameRate = SelectedFrameRate;
        settings.ViewerDownscalePercent = SelectedViewerScale;
        await _settingsStore.SaveAsync(settings);
    }
}
