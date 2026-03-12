using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Routing;
using Fennec.Client;
using Fennec.Shared.Dtos.Server;
using NodaTime;
using NodaTime.Text;
using ShadUI;
using Microsoft.Extensions.Logging;

namespace Fennec.App.ViewModels;

public partial class CreateInviteViewModel(
    IFennecClient client,
    IRouter router,
    ToastManager toastManager,
    Guid serverId,
    string instanceUrl,
    ILogger<CreateInviteViewModel> logger
) : ObservableObject
{
    public List<string> ExpiryOptions { get; } =
    [
        "Never",
        "30 minutes",
        "1 hour",
        "6 hours",
        "12 hours",
        "1 day",
        "7 days",
    ];

    [ObservableProperty]
    private string _selectedExpiry = "Never";

    [ObservableProperty]
    private string _maxUsesText = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private bool _isCreated;

    [ObservableProperty]
    private string? _inviteUrl;

    public IClipboard? Clipboard { get; set; }

    [RelayCommand]
    private async Task CreateInviteAsync()
    {
        ErrorMessage = null;

        int? maxUses = null;
        if (!string.IsNullOrWhiteSpace(MaxUsesText))
        {
            if (!int.TryParse(MaxUsesText.Trim(), CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
            {
                ErrorMessage = "Max uses must be a positive number.";
                return;
            }
            maxUses = parsed;
        }

        string? expiresAt = null;
        if (SelectedExpiry != "Never")
        {
            var duration = SelectedExpiry switch
            {
                "30 minutes" => Duration.FromMinutes(30),
                "1 hour" => Duration.FromHours(1),
                "6 hours" => Duration.FromHours(6),
                "12 hours" => Duration.FromHours(12),
                "1 day" => Duration.FromDays(1),
                "7 days" => Duration.FromDays(7),
                _ => Duration.Zero,
            };
            var expiry = SystemClock.Instance.GetCurrentInstant() + duration;
            expiresAt = InstantPattern.ExtendedIso.Format(expiry);
        }

        IsCreating = true;

        try
        {
            var response = await client.Server.CreateInviteAsync(instanceUrl, serverId, new CreateServerInviteRequestDto
            {
                ExpiresAt = expiresAt,
                MaxUses = maxUses,
            });

            InviteUrl = $"https://{instanceUrl}/invite/{response.Code}";
            IsCreated = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create invite for server {ServerId} on {Url}", serverId, instanceUrl);
            ErrorMessage = $"Failed to create invite: {ex.Message}";
        }
        finally
        {
            IsCreating = false;
        }
    }

    [RelayCommand]
    private async Task CopyInviteUrlAsync()
    {
        if (Clipboard is not null && InviteUrl is not null)
        {
            await Clipboard.SetTextAsync(InviteUrl);
            toastManager.CreateToast("Copied!")
                .WithContent("Invite link copied to clipboard.")
                .WithDelay(3)
                .ShowInfo();
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await router.NavigateBackAsync();
    }
}
