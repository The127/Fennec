using System.Text.RegularExpressions;

namespace Fennec.Api.Services;

public interface IMentionParser
{
    IEnumerable<string> ParseMentions(string content);
}

public partial class MentionParser : IMentionParser
{
    [GeneratedRegex(@"@(\w+)", RegexOptions.Compiled)]
    private static partial Regex MentionRegex();

    public IEnumerable<string> ParseMentions(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Enumerable.Empty<string>();

        return MentionRegex().Matches(content)
            .Select(m => m.Groups[1].Value)
            .Distinct();
    }
}
