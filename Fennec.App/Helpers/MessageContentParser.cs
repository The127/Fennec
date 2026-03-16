using System.Globalization;
using System.Text.RegularExpressions;

namespace Fennec.App.Helpers;

public abstract partial record MessageSegment;
public record PlainTextSegment(string Text) : MessageSegment;
public record EmojiSegment(string Text) : MessageSegment;
public record InlineCodeSegment(string Code) : MessageSegment;
public record CodeBlockSegment(string? Language, string Code) : MessageSegment;
public record LinkSegment(string Text, Uri Url) : MessageSegment;
public record SuppressedLinkSegment(string Text, Uri Url) : MessageSegment;

public static partial class MessageContentParser
{
    // Match fenced code blocks: ```lang\ncode\n``` (language is optional)
    [GeneratedRegex(@"```(\w*)\n([\s\S]*?)```", RegexOptions.Compiled)]
    private static partial Regex CodeBlockRegex();

    // Match inline code: `code` (no newlines inside)
    [GeneratedRegex(@"`([^`\n]+)`", RegexOptions.Compiled)]
    private static partial Regex InlineCodeRegex();

    // Match suppressed links: <https://...>
    [GeneratedRegex(@"<(https?://[^\s<>]+)>", RegexOptions.Compiled)]
    private static partial Regex SuppressedLinkRegex();

    // Match bare URLs
    [GeneratedRegex(@"https?://[^\s<>\)\]]+", RegexOptions.Compiled)]
    private static partial Regex UrlRegex();

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
                ParseLinkSegments(text[pos..m.Index], segments);

            segments.Add(new InlineCodeSegment(m.Groups[1].Value));
            pos = m.Index + m.Length;
        }

        if (pos < text.Length)
            ParseLinkSegments(text[pos..], segments);
    }

    private static void ParseLinkSegments(string text, List<MessageSegment> segments)
    {
        // First pass: find suppressed links <url>
        var suppressedMatches = SuppressedLinkRegex().Matches(text);
        var pos = 0;

        foreach (Match m in suppressedMatches)
        {
            if (m.Index > pos)
                ParseBareUrls(text[pos..m.Index], segments);

            var urlText = m.Groups[1].Value;
            segments.Add(new SuppressedLinkSegment(urlText, new Uri(urlText)));
            pos = m.Index + m.Length;
        }

        if (pos < text.Length)
            ParseBareUrls(text[pos..], segments);
    }

    private static void ParseBareUrls(string text, List<MessageSegment> segments)
    {
        var urlMatches = UrlRegex().Matches(text);
        var pos = 0;

        foreach (Match m in urlMatches)
        {
            if (m.Index > pos)
                segments.Add(new PlainTextSegment(text[pos..m.Index]));

            var urlText = m.Value;
            segments.Add(new LinkSegment(urlText, new Uri(urlText)));
            pos = m.Index + m.Length;
        }

        if (pos < text.Length)
            SplitEmojiRuns(text[pos..], segments);
    }

    private static void SplitEmojiRuns(string text, List<MessageSegment> segments)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var currentRun = new System.Text.StringBuilder();
        var currentIsEmoji = (bool?)null;

        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            var elementIsEmoji = !string.IsNullOrWhiteSpace(element) && EmojiHelper.IsEmoji(element);

            if (currentIsEmoji is not null && elementIsEmoji != currentIsEmoji)
            {
                // Flush current run
                if (currentRun.Length > 0)
                {
                    segments.Add(currentIsEmoji.Value
                        ? new EmojiSegment(currentRun.ToString())
                        : new PlainTextSegment(currentRun.ToString()));
                    currentRun.Clear();
                }
            }

            // Whitespace attaches to whatever run type is current, or starts a plain run
            if (string.IsNullOrWhiteSpace(element))
            {
                currentRun.Append(element);
                currentIsEmoji ??= false;
            }
            else
            {
                currentIsEmoji = elementIsEmoji;
                currentRun.Append(element);
            }
        }

        if (currentRun.Length > 0)
        {
            segments.Add(currentIsEmoji == true
                ? new EmojiSegment(currentRun.ToString())
                : new PlainTextSegment(currentRun.ToString()));
        }
    }
}
