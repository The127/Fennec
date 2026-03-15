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
        target.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        target.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(null).Properties;
        string? button = null;
        if (props.IsXButton1Pressed) button = "xbutton1";
        else if (props.IsXButton2Pressed) button = "xbutton2";
        if (button is null) return;

        var shortcutId = _keymapService.GetMouseBindings()
            .FirstOrDefault(b => b.Button == button)?.ShortcutId;
        if (shortcutId is null) return;

        foreach (var handler in _collectHandlers())
        {
            if (handler.HandleShortcut(shortcutId))
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
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
