using Avalonia.Input;

namespace Fennec.App.Shortcuts;

public record ShortcutBinding(string Id, string DisplayName, KeyGesture Gesture, ShortcutContext Context);
