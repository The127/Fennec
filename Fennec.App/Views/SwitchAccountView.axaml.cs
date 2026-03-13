using Avalonia.Markup.Xaml;
using Fennec.App.ViewModels;
using System.Threading.Tasks;

namespace Fennec.App.Views;

public partial class SwitchAccountView : Avalonia.Controls.UserControl
{
    public SwitchAccountView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SwitchAccountViewModel vm)
        {
            _ = vm.InitializeAsync();
        }
    }
}
