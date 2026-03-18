namespace Fennec.App.Domain;

public record FederatedAddress(string Username, string? InstanceUrl)
{
    public static FederatedAddress Parse(string input)
    {
        var atIndex = input.IndexOf('@');
        if (atIndex < 0)
            throw new ArgumentException("Expected format: username@instance.url", nameof(input));

        var username = input[..atIndex];
        var instanceUrl = input[(atIndex + 1)..];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(instanceUrl))
            throw new ArgumentException("Username and instance URL must not be empty", nameof(input));

        return new FederatedAddress(username, instanceUrl);
    }

    public static bool TryParse(string input, out FederatedAddress? result)
    {
        result = null;
        var atIndex = input.IndexOf('@');
        if (atIndex < 0) return false;

        var username = input[..atIndex];
        var instanceUrl = input[(atIndex + 1)..];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(instanceUrl)) return false;

        result = new FederatedAddress(username, instanceUrl);
        return true;
    }

    public override string ToString() => InstanceUrl is not null ? $"{Username}@{InstanceUrl}" : Username;
}
