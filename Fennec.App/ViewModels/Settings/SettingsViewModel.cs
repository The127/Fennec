using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Services;
using Fennec.App.Shortcuts;
using Material.Icons;

namespace Fennec.App.ViewModels.Settings;

public partial class SettingsViewModel : ObservableObject
{
    public ObservableCollection<SettingsCategory> Categories { get; } = [];

    [ObservableProperty]
    private SettingsCategory? _selectedCategory;

    public SettingsViewModel(IKeymapService keymapService, ISettingsStore settingsStore, AppSettings currentSettings, string username, string serverUrl)
    {
        Categories.Add(new SettingsCategory("Account", MaterialIconKind.Account, new AccountSettingsViewModel(username, serverUrl)));
        Categories.Add(new SettingsCategory("Appearance", MaterialIconKind.Palette, new AppearanceSettingsViewModel(settingsStore, currentSettings)));
        Categories.Add(new SettingsCategory("Keybindings", MaterialIconKind.Keyboard, new KeybindingsSettingsViewModel(keymapService)));

        SelectedCategory = Categories[0];
    }
}
