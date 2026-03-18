namespace Fennec.App.Domain;

public static class MessageLengthPolicy
{
    public const int MaxLength = 10_000;
    public const int CounterVisibleThreshold = 9_000;

    public static int CharsRemaining(string text) => MaxLength - text.Length;
    public static bool ShouldShowCounter(string text) => text.Length >= CounterVisibleThreshold;
    public static bool IsOverLimit(string text) => text.Length > MaxLength;
}
