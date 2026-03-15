using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.ViewModels;

namespace Fennec.App.Views;

public partial class ScreenShareWindow : Window
{
    public ScreenShareWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is ScreenShareWindowViewModel vm)
            {
                vm.PropertyChanged += OnVmPropertyChanged;
            }
        };

        Closed += OnWindowClosed;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScreenShareWindowViewModel.ShouldClose))
        {
            if (DataContext is ScreenShareWindowViewModel { ShouldClose: true })
                Close();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (DataContext is ScreenShareWindowViewModel vm)
        {
            vm.PropertyChanged -= OnVmPropertyChanged;
            WeakReferenceMessenger.Default.Send(new ScreenSharePopOutClosedMessage(vm.UserId));
            vm.Dispose();
        }
    }
}
