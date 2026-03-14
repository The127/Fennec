using Avalonia.Media;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

namespace Fennec.App.Services;

public record ColoredToken(string Text, Color Foreground, bool IsBold, bool IsItalic);

public class SyntaxHighlightService
{
    private readonly RegistryOptions _options;
    private readonly Registry _registry;
    private readonly Theme _theme;
    private readonly Color _defaultForeground;

    private static readonly Dictionary<string, string> LanguageAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cs"] = ".cs",
        ["csharp"] = ".cs",
        ["js"] = ".js",
        ["javascript"] = ".js",
        ["ts"] = ".ts",
        ["typescript"] = ".ts",
        ["tsx"] = ".tsx",
        ["jsx"] = ".jsx",
        ["py"] = ".py",
        ["python"] = ".py",
        ["json"] = ".json",
        ["html"] = ".html",
        ["css"] = ".css",
        ["sql"] = ".sql",
        ["rust"] = ".rs",
        ["rs"] = ".rs",
        ["go"] = ".go",
        ["java"] = ".java",
        ["bash"] = ".sh",
        ["sh"] = ".sh",
        ["shell"] = ".sh",
        ["xml"] = ".xml",
        ["yaml"] = ".yaml",
        ["yml"] = ".yaml",
        ["md"] = ".md",
        ["markdown"] = ".md",
        ["cpp"] = ".cpp",
        ["c"] = ".c",
        ["rb"] = ".rb",
        ["ruby"] = ".rb",
        ["php"] = ".php",
        ["swift"] = ".swift",
        ["kotlin"] = ".kt",
        ["kt"] = ".kt",
        ["toml"] = ".toml",
        ["dockerfile"] = ".dockerfile",
        ["docker"] = ".dockerfile",
        ["lua"] = ".lua",
        ["r"] = ".r",
        ["scala"] = ".scala",
        ["dart"] = ".dart",
        ["zig"] = ".zig",
    };

    public SyntaxHighlightService()
    {
        _options = new RegistryOptions(ThemeName.DarkPlus);
        _registry = new Registry(_options);
        _theme = _registry.GetTheme();

        // Default foreground from theme GUI colors
        var guiColors = _theme.GetGuiColorDictionary();
        _defaultForeground = guiColors.TryGetValue("editor.foreground", out var fg)
            ? ParseHexColor(fg)
            : Colors.White;
    }

    public List<ColoredToken> Tokenize(string code, string? language)
    {
        var grammar = ResolveGrammar(language);
        if (grammar is null)
            return [new ColoredToken(code, _defaultForeground, false, false)];

        var result = new List<ColoredToken>();
        var lines = code.Split('\n');
        TextMateSharp.Grammars.IStateStack? ruleStack = null;

        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                result.Add(new ColoredToken("\n", _defaultForeground, false, false));

            var line = lines[i];
            if (line.Length == 0)
                continue;

            var lineResult = grammar.TokenizeLine(line.AsMemory(), ruleStack, TimeSpan.MaxValue);
            ruleStack = lineResult.RuleStack;

            foreach (var token in lineResult.Tokens)
            {
                var startIndex = Math.Min(token.StartIndex, line.Length);
                var endIndex = Math.Min(token.EndIndex, line.Length);
                if (startIndex >= endIndex) continue;

                var tokenText = line[startIndex..endIndex];
                var fg = _defaultForeground;
                var bold = false;
                var italic = false;

                foreach (var themeRule in _theme.Match(token.Scopes))
                {
                    if (themeRule.foreground > 0)
                    {
                        fg = ParseHexColor(_theme.GetColor(themeRule.foreground));
                        break;
                    }
                }

                foreach (var themeRule in _theme.Match(token.Scopes))
                {
                    if (themeRule.fontStyle > 0)
                    {
                        bold = ((int)themeRule.fontStyle & (int)TextMateSharp.Themes.FontStyle.Bold) != 0;
                        italic = ((int)themeRule.fontStyle & (int)TextMateSharp.Themes.FontStyle.Italic) != 0;
                        break;
                    }
                }

                result.Add(new ColoredToken(tokenText, fg, bold, italic));
            }
        }

        return result;
    }

    private TextMateSharp.Grammars.IGrammar? ResolveGrammar(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return null;

        try
        {
            if (LanguageAliases.TryGetValue(language, out var ext))
            {
                var scopeName = _options.GetScopeByExtension(ext);
                if (scopeName is not null)
                    return _registry.LoadGrammar(scopeName);
            }

            // Try as extension directly
            var scope = _options.GetScopeByExtension("." + language);
            if (scope is not null)
                return _registry.LoadGrammar(scope);
        }
        catch
        {
            // Grammar not found — fall back to plain text
        }

        return null;
    }

    private static Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        return hex.Length switch
        {
            6 => Color.FromRgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16)),
            8 => Color.FromArgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16),
                Convert.ToByte(hex[6..8], 16)),
            _ => Colors.White,
        };
    }
}
