namespace Fennec.App.Validators;

using System.ComponentModel.DataAnnotations;
using Fennec.App.Domain;

public class UsernameFormatAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string str) return false;
        if (!FederatedAddress.TryParse(str, out var address)) return false;
        if (address!.InstanceUrl!.Contains('@')) return false;
        if (address.InstanceUrl.Contains("://")) return false;

        return true;
    }

    public override string FormatErrorMessage(string name)
        => $"{name} must be in the format username@instance.url";
}