using Fennec.App.Shortcuts;

namespace Fennec.App.ViewModels.Settings;

public class ShortcutBindingItem(ShortcutBinding binding)
{
    public string DisplayName { get; } = binding.DisplayName;
    public string GestureText { get; } = binding.Gesture.ToString();
    public string ContextLabel { get; } = binding.Context.ToString();
}
