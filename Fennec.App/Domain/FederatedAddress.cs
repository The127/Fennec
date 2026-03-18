namespace Fennec.App.Domain;

public record FederatedAddress(string Username, InstanceUrl? InstanceUrl)
{
    public static FederatedAddress Parse(string input)
    {
        var atIndex = input.IndexOf('@');
        if (atIndex < 0)
            throw new ArgumentException("Expected format: username@instance.url", nameof(input));

        var username = input[..atIndex];
        var instanceUrlStr = input[(atIndex + 1)..];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(instanceUrlStr))
            throw new ArgumentException("Username and instance URL must not be empty", nameof(input));

        return new FederatedAddress(username, new InstanceUrl(instanceUrlStr));
    }

    public static bool TryParse(string input, out FederatedAddress? result)
    {
        result = null;
        var atIndex = input.IndexOf('@');
        if (atIndex < 0) return false;

        var username = input[..atIndex];
        var instanceUrlStr = input[(atIndex + 1)..];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(instanceUrlStr)) return false;
        if (instanceUrlStr.Contains('@')) return false;

        result = new FederatedAddress(username, new InstanceUrl(instanceUrlStr));
        return true;
    }

    public override string ToString() => InstanceUrl is not null ? $"{Username}@{InstanceUrl}" : Username;
}
