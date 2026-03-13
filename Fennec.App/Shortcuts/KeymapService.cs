using Avalonia.Input;

namespace Fennec.App.Shortcuts;

public class KeymapService : IKeymapService
{
    private readonly Dictionary<string, ShortcutBinding> _bindings = new();
    private readonly Dictionary<string, KeyGesture> _defaults = new();

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
        Register("app.zoomIn",          "Zoom In",             "Ctrl+OemPlus",  ShortcutContext.Global);
        Register("app.zoomOut",         "Zoom Out",            "Ctrl+OemMinus", ShortcutContext.Global);
        Register("app.zoomReset",       "Reset Zoom",          "Ctrl+D0",       ShortcutContext.Global);
    }

    private void Register(string id, string displayName, string gesture, ShortcutContext context)
    {
        var keyGesture = KeyGesture.Parse(gesture);
        _defaults[id] = keyGesture;
        _bindings[id] = new ShortcutBinding(id, displayName, keyGesture, context);
    }

    public ShortcutBinding? FindBinding(KeyGesture gesture, ShortcutContext context)
    {
        // Try context-specific first, then fall back to Global
        var match = _bindings.Values.FirstOrDefault(b => b.Context == context && Matches(b.Gesture, gesture));
        if (match is not null) return match;

        if (context != ShortcutContext.Global)
            match = _bindings.Values.FirstOrDefault(b => b.Context == ShortcutContext.Global && Matches(b.Gesture, gesture));

        return match;
    }

    public ShortcutBinding? FindBindingById(string id)
        => _bindings.GetValueOrDefault(id);

    public IReadOnlyList<ShortcutBinding> GetBindings()
        => _bindings.Values.ToList().AsReadOnly();

    public IReadOnlyList<ShortcutBinding> GetBindingsForContext(ShortcutContext context)
        => _bindings.Values.Where(b => b.Context == context).ToList().AsReadOnly();

    public KeyGesture GetDefaultGesture(string id)
        => _defaults.TryGetValue(id, out var gesture)
            ? gesture
            : throw new ArgumentException($"Unknown binding ID: {id}");

    public BindingConflict? UpdateBinding(string id, KeyGesture newGesture)
    {
        if (!_bindings.TryGetValue(id, out var binding))
            throw new ArgumentException($"Unknown binding ID: {id}");

        // Check for conflicts in the same context or Global
        var conflict = _bindings.Values.FirstOrDefault(b =>
            b.Id != id &&
            Matches(b.Gesture, newGesture) &&
            (b.Context == binding.Context || b.Context == ShortcutContext.Global || binding.Context == ShortcutContext.Global));

        if (conflict is not null)
            return new BindingConflict(conflict.Id, conflict.DisplayName);

        _bindings[id] = binding with { Gesture = newGesture };
        return null;
    }

    public void ResetBinding(string id)
    {
        if (!_bindings.TryGetValue(id, out var binding))
            throw new ArgumentException($"Unknown binding ID: {id}");

        _bindings[id] = binding with { Gesture = _defaults[id] };
    }

    public void LoadOverrides(Dictionary<string, string> overrides)
    {
        foreach (var (id, gestureStr) in overrides)
        {
            if (!_bindings.ContainsKey(id)) continue;
            try
            {
                var gesture = KeyGesture.Parse(gestureStr);
                _bindings[id] = _bindings[id] with { Gesture = gesture };
            }
            catch
            {
                // Skip invalid gestures
            }
        }
    }

    private static bool Matches(KeyGesture a, KeyGesture b)
        => a.Key == b.Key && a.KeyModifiers == b.KeyModifiers;
}
