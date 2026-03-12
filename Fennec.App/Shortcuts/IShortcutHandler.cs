namespace Fennec.App.Shortcuts;

public interface IShortcutHandler
{
    ShortcutContext ShortcutContext { get; }
    bool HandleShortcut(string shortcutId);
}
