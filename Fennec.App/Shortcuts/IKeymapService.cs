using Avalonia.Input;

namespace Fennec.App.Shortcuts;

public interface IKeymapService
{
    ShortcutBinding? FindBinding(KeyGesture gesture, ShortcutContext context);
    ShortcutBinding? FindBindingById(string id);
    IReadOnlyList<ShortcutBinding> GetBindings();
    IReadOnlyList<ShortcutBinding> GetBindingsForContext(ShortcutContext context);
    KeyGesture GetDefaultGesture(string id);
    BindingConflict? UpdateBinding(string id, KeyGesture newGesture);
    void ResetBinding(string id);
    void LoadOverrides(Dictionary<string, string> overrides);
}

public record BindingConflict(string ConflictingId, string ConflictingDisplayName);
