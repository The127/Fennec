using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Shortcuts;

namespace Fennec.App.ViewModels.Settings;

public partial class ShortcutBindingItem : ObservableObject
{
    public ShortcutBindingItem(ShortcutBinding binding, bool isModified)
    {
        Id = binding.Id;
        DisplayName = binding.DisplayName;
        GestureText = binding.Gesture.ToString();
        ContextLabel = binding.Context.ToString();
        IsModified = isModified;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string ContextLabel { get; }

    [ObservableProperty]
    private string _gestureText;

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private bool _isModified;
}
