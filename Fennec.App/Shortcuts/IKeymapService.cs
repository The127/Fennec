using Avalonia.Input;

namespace Fennec.App.Shortcuts;

public interface IKeymapService
{
    ShortcutBinding? FindBinding(KeyGesture gesture, ShortcutContext context);
    IReadOnlyList<ShortcutBinding> GetBindings();
    IReadOnlyList<ShortcutBinding> GetBindingsForContext(ShortcutContext context);
}
