using System.Text.RegularExpressions;
using Fennec.App.Models;

namespace Fennec.App.Formatting;

public static class ShortcodeReplacer
{
    public static string Replace(string text)
    {
        return Regex.Replace(text, @":([a-z0-9_]+):", match =>
            EmojiDatabase.ByShortcode.TryGetValue(match.Groups[1].Value, out var entry)
                ? entry.Unicode
                : match.Value);
    }
}
