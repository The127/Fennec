namespace Fennec.App.Validators;

using System.ComponentModel.DataAnnotations;

public class UsernameFormatAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string str) return false;
        
        var parts = str.Split('@');
        if (parts.Length != 2) return false;
        
        var username = parts[0];
        var instanceUrl = parts[1];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(instanceUrl)) return false;
        if (instanceUrl.Contains("://")) return false;

        return true;
    }

    public override string FormatErrorMessage(string name)
        => $"{name} must be in the format username@instance.url";
}