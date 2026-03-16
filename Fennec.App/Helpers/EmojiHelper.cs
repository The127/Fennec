using System.Globalization;

namespace Fennec.App.Helpers;

public static class EmojiHelper
{
    public static bool IsAllEmoji(string text)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var hasNonWhitespace = false;
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            if (string.IsNullOrWhiteSpace(element))
                continue;
            hasNonWhitespace = true;
            if (!IsEmoji(element))
                return false;
        }
        return hasNonWhitespace;
    }

    public static bool IsEmoji(string textElement)
    {
        foreach (var rune in textElement.EnumerateRunes())
        {
            var value = rune.Value;
            // Allow variation selectors and zero-width joiner (combining chars in emoji sequences)
            if (value == 0xFE0F || value == 0xFE0E || value == 0x200D)
                continue;
            // Allow skin tone modifiers
            if (value >= 0x1F3FB && value <= 0x1F3FF)
                continue;
            // Allow tag characters (flag sequences)
            if (value >= 0xE0020 && value <= 0xE007F)
                continue;
            // Common emoji ranges
            if (value >= 0x1F600 && value <= 0x1F64F) continue; // Emoticons
            if (value >= 0x1F300 && value <= 0x1F5FF) continue; // Misc symbols & pictographs
            if (value >= 0x1F680 && value <= 0x1F6FF) continue; // Transport & map
            if (value >= 0x1F900 && value <= 0x1F9FF) continue; // Supplemental symbols
            if (value >= 0x1FA00 && value <= 0x1FA6F) continue; // Chess symbols
            if (value >= 0x1FA70 && value <= 0x1FAFF) continue; // Symbols extended-A
            if (value >= 0x2600 && value <= 0x26FF) continue;   // Misc symbols
            if (value >= 0x2700 && value <= 0x27BF) continue;   // Dingbats
            if (value >= 0x231A && value <= 0x23F3) continue;   // Misc technical
            if (value >= 0x2934 && value <= 0x2935) continue;   // Arrows
            if (value >= 0x25AA && value <= 0x25FE) continue;   // Geometric shapes
            if (value >= 0x2B05 && value <= 0x2B55) continue;   // Arrows & shapes
            if (value >= 0x3030 && value <= 0x303D) continue;   // CJK symbols
            if (value == 0x00A9 || value == 0x00AE) continue;   // (C) (R)
            if (value == 0x2122 || value == 0x2139) continue;   // TM, info
            if (value >= 0x23E9 && value <= 0x23FA) continue;   // Media controls
            if (value >= 0x200D && value <= 0x200D) continue;   // ZWJ (already handled)
            if (value == 0x20E3) continue;                        // Combining enclosing keycap
            // Digits, * and # only as part of keycap sequences (multi-rune elements like 1️⃣)
            if ((value >= 0x0030 && value <= 0x0039) || value == 0x002A || value == 0x0023)
            {
                if (textElement.EnumerateRunes().Count() > 1) continue;
                return false;
            }
            if (value >= 0x1F1E0 && value <= 0x1F1FF) continue; // Regional indicators (flags)
            return false;
        }
        return true;
    }
}
