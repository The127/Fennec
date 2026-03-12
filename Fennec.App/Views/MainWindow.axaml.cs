using Avalonia.Controls;
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
}