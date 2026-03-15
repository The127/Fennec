using Fennec.App.Helpers;

namespace Fennec.App.Tests.Helpers;

public class MessageContentParserTests
{
    [Fact]
    public void Parse_NoUrls_ReturnsOnlyPlainText()
    {
        var result = MessageContentParser.Parse("hello world");
        var segment = Assert.Single(result);
        Assert.IsType<PlainTextSegment>(segment);
        Assert.Equal("hello world", ((PlainTextSegment)segment).Text);
    }

    [Fact]
    public void Parse_SingleUrlInMiddle_ReturnsPlainTextLinkPlainText()
    {
        var result = MessageContentParser.Parse("check https://example.com out");
        Assert.Equal(3, result.Count);
        Assert.IsType<PlainTextSegment>(result[0]);
        Assert.Equal("check ", ((PlainTextSegment)result[0]).Text);
        Assert.IsType<LinkSegment>(result[1]);
        Assert.Equal("https://example.com", ((LinkSegment)result[1]).Text);
        Assert.Equal(new Uri("https://example.com"), ((LinkSegment)result[1]).Url);
        Assert.IsType<PlainTextSegment>(result[2]);
        Assert.Equal(" out", ((PlainTextSegment)result[2]).Text);
    }

    [Fact]
    public void Parse_UrlAtStart_ReturnsLinkThenPlainText()
    {
        var result = MessageContentParser.Parse("https://example.com is cool");
        Assert.Equal(2, result.Count);
        Assert.IsType<LinkSegment>(result[0]);
        Assert.Equal("https://example.com", ((LinkSegment)result[0]).Text);
        Assert.IsType<PlainTextSegment>(result[1]);
    }

    [Fact]
    public void Parse_UrlAtEnd_ReturnsPlainTextThenLink()
    {
        var result = MessageContentParser.Parse("visit https://example.com");
        Assert.Equal(2, result.Count);
        Assert.IsType<PlainTextSegment>(result[0]);
        Assert.IsType<LinkSegment>(result[1]);
    }

    [Fact]
    public void Parse_MultipleUrls_ReturnsMultipleLinkSegments()
    {
        var result = MessageContentParser.Parse("see https://a.com and https://b.com");
        Assert.Equal(4, result.Count);
        Assert.IsType<PlainTextSegment>(result[0]);
        Assert.IsType<LinkSegment>(result[1]);
        Assert.IsType<PlainTextSegment>(result[2]);
        Assert.IsType<LinkSegment>(result[3]);
    }

    [Fact]
    public void Parse_UrlInsideCodeBlock_RemainsUntouched()
    {
        var result = MessageContentParser.Parse("```\nhttps://example.com\n```");
        var segment = Assert.Single(result);
        Assert.IsType<CodeBlockSegment>(segment);
    }

    [Fact]
    public void Parse_UrlInsideInlineCode_RemainsUntouched()
    {
        var result = MessageContentParser.Parse("`https://example.com`");
        var segment = Assert.Single(result);
        Assert.IsType<InlineCodeSegment>(segment);
    }

    [Fact]
    public void Parse_SuppressedLink_ReturnsSuppressedLinkSegment()
    {
        var result = MessageContentParser.Parse("check <https://example.com> out");
        Assert.Equal(3, result.Count);
        Assert.IsType<PlainTextSegment>(result[0]);
        Assert.Equal("check ", ((PlainTextSegment)result[0]).Text);
        Assert.IsType<SuppressedLinkSegment>(result[1]);
        Assert.Equal("https://example.com", ((SuppressedLinkSegment)result[1]).Text);
        Assert.IsType<PlainTextSegment>(result[2]);
    }

    [Fact]
    public void Parse_UrlWithQueryString_IncludesQueryString()
    {
        var result = MessageContentParser.Parse("https://example.com/path?key=value&foo=bar");
        var segment = Assert.Single(result);
        Assert.IsType<LinkSegment>(segment);
        Assert.Equal("https://example.com/path?key=value&foo=bar", ((LinkSegment)segment).Text);
    }

    [Fact]
    public void Parse_UrlWithFragment_IncludesFragment()
    {
        var result = MessageContentParser.Parse("https://example.com/page#section");
        var segment = Assert.Single(result);
        Assert.IsType<LinkSegment>(segment);
        Assert.Equal("https://example.com/page#section", ((LinkSegment)segment).Text);
    }

    [Fact]
    public void Parse_HttpUrl_DetectedAsLink()
    {
        var result = MessageContentParser.Parse("http://example.com");
        var segment = Assert.Single(result);
        Assert.IsType<LinkSegment>(segment);
    }

    [Fact]
    public void Parse_OnlyUrl_ReturnsSingleLinkSegment()
    {
        var result = MessageContentParser.Parse("https://example.com");
        var segment = Assert.Single(result);
        Assert.IsType<LinkSegment>(segment);
        Assert.Equal("https://example.com", ((LinkSegment)segment).Text);
    }
}
