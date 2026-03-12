using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Shortcuts;
using Material.Icons;

namespace Fennec.App.ViewModels.Settings;

public partial class SettingsViewModel : ObservableObject
{
    private readonly Action _closeAction;

    public ObservableCollection<SettingsCategory> Categories { get; } = [];

    [ObservableProperty]
    private SettingsCategory? _selectedCategory;

    public SettingsViewModel(IKeymapService keymapService, string username, string serverUrl, Action closeAction)
    {
        _closeAction = closeAction;

        Categories.Add(new SettingsCategory("Account", MaterialIconKind.Account, new AccountSettingsViewModel(username, serverUrl)));
        Categories.Add(new SettingsCategory("Appearance", MaterialIconKind.Palette, new AppearanceSettingsViewModel()));
        Categories.Add(new SettingsCategory("Keybindings", MaterialIconKind.Keyboard, new KeybindingsSettingsViewModel(keymapService)));

        SelectedCategory = Categories[0];
    }

    [RelayCommand]
    private void Close()
    {
        _closeAction();
    }
}
