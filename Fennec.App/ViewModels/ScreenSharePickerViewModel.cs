using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Services;
using ShadUI;

namespace Fennec.App.ViewModels;

public record ScreenSharePickerResult(CaptureTarget Target, string Resolution, int BitrateKbps, int FrameRate);

public partial class ScreenSharePickerViewModel : ObservableObject
{
    private readonly DialogManager _dialogManager;
    private readonly ISettingsStore _settingsStore;

    public ObservableCollection<CaptureTarget> Targets { get; }

    [ObservableProperty]
    private CaptureTarget? _selectedTarget;

    public List<string> ResolutionOptions { get; } = ["720p", "1080p", "1440p", "Native"];
    public List<int> FrameRateOptions { get; } = [15, 30, 60];

    [ObservableProperty]
    private string _selectedResolution;

    [ObservableProperty]
    private int _bitrateKbps;

    [ObservableProperty]
    private int _selectedFrameRate;

    public ScreenSharePickerResult? Result { get; private set; }

    public ScreenSharePickerViewModel(DialogManager dialogManager, ISettingsStore settingsStore, AppSettings currentSettings, List<CaptureTarget> targets)
    {
        _dialogManager = dialogManager;
        _settingsStore = settingsStore;
        Targets = new(targets);

        _selectedResolution = ResolutionOptions.Contains(currentSettings.ScreenShareResolution)
            ? currentSettings.ScreenShareResolution
            : "1080p";
        _bitrateKbps = Math.Clamp(currentSettings.ScreenShareBitrateKbps, 500, 50_000);
        _selectedFrameRate = FrameRateOptions.Contains(currentSettings.ScreenShareFrameRate)
            ? currentSettings.ScreenShareFrameRate
            : 30;
    }

    [RelayCommand]
    private async Task Confirm()
    {
        if (SelectedTarget is null) return;
        Result = new ScreenSharePickerResult(SelectedTarget, SelectedResolution, BitrateKbps, SelectedFrameRate);

        // Persist chosen values as new defaults
        var settings = await _settingsStore.LoadAsync();
        settings.ScreenShareResolution = SelectedResolution;
        settings.ScreenShareBitrateKbps = BitrateKbps;
        settings.ScreenShareFrameRate = SelectedFrameRate;
        await _settingsStore.SaveAsync(settings);

        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }
}
