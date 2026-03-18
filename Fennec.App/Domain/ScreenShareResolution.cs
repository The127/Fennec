namespace Fennec.App.Domain;

public record ScreenShareResolution(string Value)
{
    public static readonly ScreenShareResolution P720 = new("720p");
    public static readonly ScreenShareResolution P1080 = new("1080p");
    public static readonly ScreenShareResolution P1440 = new("1440p");
    public static readonly ScreenShareResolution Native = new("Native");

    public static IReadOnlyList<ScreenShareResolution> All { get; } = [P720, P1080, P1440, Native];

    public override string ToString() => Value;

    public static ScreenShareResolution FromValue(string? value) =>
        All.FirstOrDefault(r => r.Value == value) ?? P1080;
}
