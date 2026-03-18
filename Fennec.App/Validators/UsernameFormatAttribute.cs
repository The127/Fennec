namespace Fennec.App.Validators;

using System.ComponentModel.DataAnnotations;
using Fennec.App.Domain;

public class UsernameFormatAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string str) return false;
        var rawInstanceUrl = str.Contains('@') ? str[(str.IndexOf('@') + 1)..] : null;
        if (rawInstanceUrl is null || rawInstanceUrl.Contains("://")) return false;
        return FederatedAddress.TryParse(str, out _);
    }

    public override string FormatErrorMessage(string name)
        => $"{name} must be in the format username@instance.url";
}