using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Services;
using ShadUI;

namespace Fennec.App.ViewModels;

public partial class ScreenSharePickerViewModel(DialogManager dialogManager, List<CaptureTarget> targets) : ObservableObject
{
    public ObservableCollection<CaptureTarget> Targets { get; } = new(targets);

    [ObservableProperty]
    private CaptureTarget? _selectedTarget;

    public CaptureTarget? Result { get; private set; }

    [RelayCommand]
    private void Confirm()
    {
        if (SelectedTarget is null) return;
        Result = SelectedTarget;
        dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [RelayCommand]
    private void Cancel()
    {
        dialogManager.Close(this);
    }
}
