using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Fennec.App.ViewModels;

public partial class TestViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Message))]
    private int _counter = 0;
    
    public string Message => $"This button has been pressed {Counter} times.";
    
    [RelayCommand]
    public void Increment() => Counter++;
}