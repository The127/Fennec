using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.Shared.Models;
using ShadUI;

namespace Fennec.App.ViewModels;

public partial class CreateChannelDialogViewModel(DialogManager dialogManager) : ObservableObject
{
    [ObservableProperty]
    private string _channelName = string.Empty;

    [ObservableProperty]
    private bool _isTextOnly;

    public ChannelType ChannelType => IsTextOnly ? ChannelType.TextOnly : ChannelType.TextAndVoice;

    [RelayCommand]
    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(ChannelName)) return;

        dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [RelayCommand]
    private void Cancel()
    {
        dialogManager.Close(this);
    }
}
