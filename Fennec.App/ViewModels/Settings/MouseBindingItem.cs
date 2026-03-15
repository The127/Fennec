using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Shortcuts;

namespace Fennec.App.ViewModels.Settings;

public partial class MouseBindingItem : ObservableObject
{
    public string Button { get; }
    public string DisplayName { get; }

    [ObservableProperty]
    private ShortcutBinding? _selectedBinding;

    [ObservableProperty]
    private bool _isModified;

    public MouseBindingItem(MouseButtonBinding binding, IReadOnlyList<ShortcutBinding> allBindings, bool isModified)
    {
        Button = binding.Button;
        DisplayName = binding.DisplayName;
        _selectedBinding = allBindings.FirstOrDefault(b => b.Id == binding.ShortcutId);
        IsModified = isModified;
    }
}
