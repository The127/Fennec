using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Services;
using Fennec.App.Shortcuts;
using Material.Icons;

namespace Fennec.App.ViewModels.Settings;

public partial class SettingsViewModel : ObservableObject, IRecipient<ZoomChangedMessage>
{
    public ObservableCollection<SettingsCategory> Categories { get; } = [];

    [ObservableProperty]
    private SettingsCategory? _selectedCategory;

    [ObservableProperty]
    private double _zoomLevel;

    public SettingsViewModel(IMessenger messenger, IKeymapService keymapService, ISettingsStore settingsStore, AppSettings currentSettings, string username, string serverUrl)
    {
        ZoomLevel = currentSettings.ZoomLevel;
        Categories.Add(new SettingsCategory("Account", MaterialIconKind.Account, new AccountSettingsViewModel(username, serverUrl)));
        Categories.Add(new SettingsCategory("Appearance", MaterialIconKind.Palette, new AppearanceSettingsViewModel(settingsStore, currentSettings)));
        Categories.Add(new SettingsCategory("Keybindings", MaterialIconKind.Keyboard, new KeybindingsSettingsViewModel(keymapService, settingsStore)));

        SelectedCategory = Categories[0];
        messenger.Register(this);
    }

    public void Receive(ZoomChangedMessage message)
    {
        ZoomLevel = message.ZoomLevel;
    }
}
