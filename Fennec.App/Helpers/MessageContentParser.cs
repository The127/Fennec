using System.Text.RegularExpressions;

namespace Fennec.App.Helpers;

public abstract partial record MessageSegment;
public record PlainTextSegment(string Text) : MessageSegment;
public record InlineCodeSegment(string Code) : MessageSegment;
public record CodeBlockSegment(string? Language, string Code) : MessageSegment;

public static partial class MessageContentParser
{
    // Match fenced code blocks: ```lang\ncode\n``` (language is optional)
    [GeneratedRegex(@"```(\w*)\n([\s\S]*?)```", RegexOptions.Compiled)]
    private static partial Regex CodeBlockRegex();

    // Match inline code: `code` (no newlines inside)
    [GeneratedRegex(@"`([^`\n]+)`", RegexOptions.Compiled)]
    private static partial Regex InlineCodeRegex();

    public static List<MessageSegment> Parse(string content)
    {
        var segments = new List<MessageSegment>();
        var codeBlocks = CodeBlockRegex().Matches(content);

        var pos = 0;
        foreach (Match block in codeBlocks)
        {
            if (block.Index > pos)
                ParseInlineSegments(content[pos..block.Index], segments);

            var lang = block.Groups[1].Value;
            var code = block.Groups[2].Value.TrimEnd('\n');
            segments.Add(new CodeBlockSegment(string.IsNullOrEmpty(lang) ? null : lang, code));
            pos = block.Index + block.Length;
        }

        if (pos < content.Length)
            ParseInlineSegments(content[pos..], segments);

        return segments;
    }

    private static void ParseInlineSegments(string text, List<MessageSegment> segments)
    {
        var inlineMatches = InlineCodeRegex().Matches(text);
        var pos = 0;

        foreach (Match m in inlineMatches)
        {
            if (m.Index > pos)
                segments.Add(new PlainTextSegment(text[pos..m.Index]));

            segments.Add(new InlineCodeSegment(m.Groups[1].Value));
            pos = m.Index + m.Length;
        }

        if (pos < text.Length)
            segments.Add(new PlainTextSegment(text[pos..]));
    }
}
