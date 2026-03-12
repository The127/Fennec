using Avalonia.Input;
using Avalonia.Interactivity;

namespace Fennec.App.Shortcuts;

public class ShortcutDispatcher
{
    private readonly IKeymapService _keymapService;
    private readonly Func<IEnumerable<IShortcutHandler>> _collectHandlers;

    public ShortcutDispatcher(IKeymapService keymapService, Func<IEnumerable<IShortcutHandler>> collectHandlers)
    {
        _keymapService = keymapService;
        _collectHandlers = collectHandlers;
    }

    public void Attach(InputElement target)
    {
        target.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled) return;

        var gesture = new KeyGesture(e.Key, e.KeyModifiers);
        var handlers = _collectHandlers().ToList();

        // Walk handlers innermost-first (list is expected in that order)
        foreach (var handler in handlers)
        {
            var binding = _keymapService.FindBinding(gesture, handler.ShortcutContext);
            if (binding is not null && handler.HandleShortcut(binding.Id))
            {
                e.Handled = true;
                return;
            }
        }
    }
}
