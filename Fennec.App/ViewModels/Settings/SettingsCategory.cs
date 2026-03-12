using CommunityToolkit.Mvvm.ComponentModel;
using Material.Icons;

namespace Fennec.App.ViewModels.Settings;

public class SettingsCategory(string label, MaterialIconKind icon, ObservableObject viewModel)
{
    public string Label { get; } = label;
    public MaterialIconKind Icon { get; } = icon;
    public ObservableObject ViewModel { get; } = viewModel;
}
