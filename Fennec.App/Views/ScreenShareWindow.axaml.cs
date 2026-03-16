using Avalonia.Controls;
using Avalonia.Input;
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
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F3 && DataContext is ScreenShareWindowViewModel vm)
        {
            vm.ShowDebugOverlay = !vm.ShowDebugOverlay;
            e.Handled = true;
        }
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
