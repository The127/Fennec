using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Shortcuts;

namespace Fennec.App.ViewModels.Settings;

public partial class KeybindingsSettingsViewModel : ObservableObject
{
    public ObservableCollection<ShortcutBindingItem> Bindings { get; } = [];

    public KeybindingsSettingsViewModel(IKeymapService keymapService)
    {
        foreach (var binding in keymapService.GetBindings())
        {
            Bindings.Add(new ShortcutBindingItem(binding));
        }
    }
}
