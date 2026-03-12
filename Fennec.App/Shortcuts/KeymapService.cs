using Avalonia.Input;

namespace Fennec.App.Shortcuts;

public class KeymapService : IKeymapService
{
    private readonly List<ShortcutBinding> _bindings = [];

    public KeymapService()
    {
        Register("app.toggleTheme",     "Toggle Theme",       "Ctrl+Shift+T", ShortcutContext.Global);
        Register("app.openSettings",    "Open Settings",      "Ctrl+OemComma", ShortcutContext.Global);
        Register("nav.dashboard",       "Go to Dashboard",    "Ctrl+D1",      ShortcutContext.MainApp);
        Register("nav.friends",         "Go to Friends",      "Ctrl+D2",      ShortcutContext.MainApp);
        Register("nav.calls",           "Go to Calls",        "Ctrl+D3",      ShortcutContext.MainApp);
        Register("nav.add",             "Add/Join Server",    "Ctrl+N",       ShortcutContext.MainApp);
        Register("nav.quickNav",        "Quick Navigation",    "Ctrl+K",       ShortcutContext.MainApp);
        Register("server.focusMessage", "Focus Message Input", "Escape",       ShortcutContext.Server);
        Register("server.openEmoji",    "Open Emoji Picker",   "Ctrl+E",       ShortcutContext.Server);
        Register("server.attachFile",   "Attach File",         "Ctrl+Shift+A", ShortcutContext.Server);
        Register("nav.focusSearch",     "Focus Search",        "Ctrl+F",       ShortcutContext.MainApp);
    }

    private void Register(string id, string displayName, string gesture, ShortcutContext context)
    {
        _bindings.Add(new ShortcutBinding(id, displayName, KeyGesture.Parse(gesture), context));
    }

    public ShortcutBinding? FindBinding(KeyGesture gesture, ShortcutContext context)
    {
        // Try context-specific first, then fall back to Global
        var match = _bindings.FirstOrDefault(b => b.Context == context && Matches(b.Gesture, gesture));
        if (match is not null) return match;

        if (context != ShortcutContext.Global)
            match = _bindings.FirstOrDefault(b => b.Context == ShortcutContext.Global && Matches(b.Gesture, gesture));

        return match;
    }

    public IReadOnlyList<ShortcutBinding> GetBindings() => _bindings.AsReadOnly();

    public IReadOnlyList<ShortcutBinding> GetBindingsForContext(ShortcutContext context)
        => _bindings.Where(b => b.Context == context).ToList().AsReadOnly();

    private static bool Matches(KeyGesture a, KeyGesture b)
        => a.Key == b.Key && a.KeyModifiers == b.KeyModifiers;
}
