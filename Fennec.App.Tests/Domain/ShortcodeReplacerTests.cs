using Fennec.App.Domain;

namespace Fennec.App.Tests.Domain;

public class ShortcodeReplacerTests
{
    [Fact]
    public void Known_shortcode_replaced_with_unicode()
    {
        Assert.Equal("\U0001F600", ShortcodeReplacer.Replace(":grinning:"));
    }

    [Fact]
    public void Unknown_shortcode_left_as_is()
    {
        Assert.Equal(":not_a_real_shortcode:", ShortcodeReplacer.Replace(":not_a_real_shortcode:"));
    }

    [Fact]
    public void Multiple_shortcodes_in_one_string()
    {
        var result = ShortcodeReplacer.Replace(":grinning: hello :smiley:");
        Assert.Equal("\U0001F600 hello \U0001F603", result);
    }

    [Fact]
    public void Empty_string_returned_unchanged()
    {
        Assert.Equal("", ShortcodeReplacer.Replace(""));
    }
}
