using CommunityToolkit.Mvvm.ComponentModel;

namespace Fennec.App.ViewModels;

public partial class LoadingViewModel : ObservableObject
{
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private double _updateProgress;
    [ObservableProperty] private bool _isUpdating;
}
