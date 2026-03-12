using Avalonia.Controls;
using Fennec.App.Shortcuts;
using Fennec.App.ViewModels;
using ShadUI;

namespace Fennec.App.Views;

public partial class MainWindow : ShadUI.Window
{
    public MainWindow()
    {
        InitializeComponent();

        var dialogHost = this.FindControl<DialogHost>("PART_DialogHost");
        if (dialogHost is not null)
        {
            dialogHost.Owner = this;
        }
    }

    public void AttachShortcutDispatcher(IKeymapService keymapService)
    {
        var dispatcher = new ShortcutDispatcher(keymapService, CollectHandlers);
        dispatcher.Attach(this);
    }

    private IEnumerable<IShortcutHandler> CollectHandlers()
    {
        if (DataContext is not AppShellViewModel shell)
            yield break;

        // Walk from innermost to outermost
        if (shell.CurrentViewModel is MainAppViewModel mainApp)
        {
            if (mainApp.Router.CurrentViewModel is IShortcutHandler innerHandler)
                yield return innerHandler;

            if (mainApp is IShortcutHandler mainHandler)
                yield return mainHandler;
        }
    }
}
