using System.Collections.ObjectModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Services;
using Fennec.App.Shortcuts;

namespace Fennec.App.ViewModels.Settings;

public partial class KeybindingsSettingsViewModel : ObservableObject
{
    private readonly IKeymapService _keymapService;
    private readonly ISettingsStore _settingsStore;

    [ObservableProperty]
    private string? _conflictMessage;

    public ObservableCollection<ShortcutBindingItem> Bindings { get; } = [];

    public KeybindingsSettingsViewModel(IKeymapService keymapService, ISettingsStore settingsStore)
    {
        _keymapService = keymapService;
        _settingsStore = settingsStore;

        foreach (var binding in keymapService.GetBindings())
        {
            var defaultGesture = keymapService.GetDefaultGesture(binding.Id);
            var isModified = !GesturesMatch(binding.Gesture, defaultGesture);
            Bindings.Add(new ShortcutBindingItem(binding, isModified));
        }
    }

    [RelayCommand]
    private void StartCapture(ShortcutBindingItem item)
    {
        // Cancel any other active capture
        foreach (var b in Bindings)
            b.IsCapturing = false;

        ConflictMessage = null;
        item.IsCapturing = true;
    }

    [RelayCommand]
    private void CancelCapture(ShortcutBindingItem item)
    {
        item.IsCapturing = false;
        ConflictMessage = null;
    }

    public void HandleCapturedGesture(ShortcutBindingItem item, KeyGesture gesture)
    {
        var conflict = _keymapService.UpdateBinding(item.Id, gesture);
        if (conflict is not null)
        {
            ConflictMessage = $"\"{gesture}\" is already used by \"{conflict.ConflictingDisplayName}\"";
            return;
        }

        item.GestureText = gesture.ToString();
        item.IsCapturing = false;
        item.IsModified = !GesturesMatch(gesture, _keymapService.GetDefaultGesture(item.Id));
        ConflictMessage = null;

        _ = SaveBindingsAsync();
    }

    [RelayCommand]
    private void ResetBinding(ShortcutBindingItem item)
    {
        _keymapService.ResetBinding(item.Id);
        var defaultGesture = _keymapService.GetDefaultGesture(item.Id);
        item.GestureText = defaultGesture.ToString();
        item.IsModified = false;
        item.IsCapturing = false;
        ConflictMessage = null;

        _ = SaveBindingsAsync();
    }

    private async Task SaveBindingsAsync()
    {
        var settings = await _settingsStore.LoadAsync();

        var overrides = new Dictionary<string, string>();
        foreach (var binding in _keymapService.GetBindings())
        {
            var defaultGesture = _keymapService.GetDefaultGesture(binding.Id);
            if (!GesturesMatch(binding.Gesture, defaultGesture))
            {
                overrides[binding.Id] = binding.Gesture.ToString();
            }
        }

        settings.KeyBindings = overrides.Count > 0 ? overrides : null;
        await _settingsStore.SaveAsync(settings);
    }

    private static bool GesturesMatch(KeyGesture a, KeyGesture b)
        => a.Key == b.Key && a.KeyModifiers == b.KeyModifiers;
}
