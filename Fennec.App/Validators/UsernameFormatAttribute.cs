namespace Fennec.App.Validators;

using System.ComponentModel.DataAnnotations;

public class UsernameFormatAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string str) return false;
        return str.Contains('@'); // simple check
    }

    public override string FormatErrorMessage(string name)
        => $"{name} must be in the format username@instance.url";
}