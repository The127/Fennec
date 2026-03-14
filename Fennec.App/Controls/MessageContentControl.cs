using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Fennec.App.Helpers;
using Fennec.App.Services;
using Material.Icons;
using Material.Icons.Avalonia;

namespace Fennec.App.Controls;

public class MessageContentControl : UserControl
{
    public static readonly StyledProperty<string?> MessageTextProperty =
        AvaloniaProperty.Register<MessageContentControl, string?>(nameof(MessageText));

    public static readonly StyledProperty<bool> IsEmojiOnlyProperty =
        AvaloniaProperty.Register<MessageContentControl, bool>(nameof(IsEmojiOnly));

    private static readonly Lazy<SyntaxHighlightService> Highlighter = new();

    private static readonly FontFamily MonoFont = new("Cascadia Code, Consolas, Menlo, Monaco, monospace");
    private static readonly FontFamily ContentFont = new("Inter, fonts:NotoColorEmoji#Noto Color Emoji");

    public string? MessageText
    {
        get => GetValue(MessageTextProperty);
        set => SetValue(MessageTextProperty, value);
    }

    public bool IsEmojiOnly
    {
        get => GetValue(IsEmojiOnlyProperty);
        set => SetValue(IsEmojiOnlyProperty, value);
    }

    static MessageContentControl()
    {
        MessageTextProperty.Changed.AddClassHandler<MessageContentControl>((c, _) => c.Rebuild());
        IsEmojiOnlyProperty.Changed.AddClassHandler<MessageContentControl>((c, _) => c.Rebuild());
    }

    private static void BindBrushToResource(StyledElement element, AvaloniaProperty property, string resourceKey)
    {
        element.Bind(property, element.GetResourceObservable(resourceKey));
    }

    private void Rebuild()
    {
        var content = MessageText;
        if (string.IsNullOrEmpty(content))
        {
            base.Content = null;
            return;
        }

        if (IsEmojiOnly)
        {
            base.Content = new TextBlock
            {
                Text = content,
                FontSize = 30,
                FontFamily = ContentFont,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            return;
        }

        var segments = MessageContentParser.Parse(content);

        // If single plain text segment, render simply
        if (segments is [PlainTextSegment plain])
        {
            base.Content = new TextBlock
            {
                Text = plain.Text,
                FontSize = 15,
                FontFamily = ContentFont,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            return;
        }

        var panel = new StackPanel { Spacing = 4 };

        // Group consecutive inline segments into TextBlocks
        var inlineBuffer = new List<MessageSegment>();

        foreach (var segment in segments)
        {
            if (segment is CodeBlockSegment codeBlock)
            {
                FlushInlines(inlineBuffer, panel);
                panel.Children.Add(BuildCodeBlock(codeBlock));
            }
            else
            {
                inlineBuffer.Add(segment);
            }
        }

        FlushInlines(inlineBuffer, panel);
        base.Content = panel;
    }

    private static void FlushInlines(List<MessageSegment> buffer, StackPanel panel)
    {
        if (buffer.Count == 0) return;

        var tb = new TextBlock
        {
            FontSize = 15,
            FontFamily = ContentFont,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        foreach (var seg in buffer)
        {
            switch (seg)
            {
                case PlainTextSegment plain:
                    tb.Inlines!.Add(new Run(plain.Text));
                    break;
                case InlineCodeSegment inline:
                    var codeSpan = new Span
                    {
                        FontFamily = MonoFont,
                        FontSize = 13,
                    };
                    BindBrushToResource(codeSpan, Span.BackgroundProperty, "InlineCodeBackgroundBrush");
                    codeSpan.Inlines!.Add(new Run(" " + inline.Code + " "));
                    tb.Inlines!.Add(codeSpan);
                    break;
            }
        }

        panel.Children.Add(tb);
        buffer.Clear();
    }

    private Control BuildCodeBlock(CodeBlockSegment segment)
    {
        var outerBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(0),
            Margin = new Thickness(0, 4),
            BorderThickness = new Thickness(1),
        };
        BindBrushToResource(outerBorder, Border.BackgroundProperty, "CodeBlockBackgroundBrush");
        BindBrushToResource(outerBorder, Border.BorderBrushProperty, "CodeBlockBorderBrush");

        var container = new DockPanel();

        // Header with language label + copy button
        var header = new DockPanel
        {
            Margin = new Thickness(12, 6, 6, 0),
        };

        if (segment.Language is not null)
        {
            var langLabel = new TextBlock
            {
                Text = segment.Language,
                FontSize = 12,
                FontFamily = MonoFont,
                Opacity = 0.6,
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(langLabel, Dock.Left);
            header.Children.Add(langLabel);
        }

        var copyButton = new Button
        {
            Content = new MaterialIcon { Kind = MaterialIconKind.ContentCopy, Width = 14, Height = 14 },
            Padding = new Thickness(4),
            Cursor = new Cursor(StandardCursorType.Hand),
            Opacity = 0.5,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Classes = { "ghost" },
        };
        var codeText = segment.Code;
        copyButton.Click += async (_, _) =>
        {
            var topLevel = TopLevel.GetTopLevel(copyButton);
            if (topLevel?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(codeText);
        };
        DockPanel.SetDock(copyButton, Dock.Right);
        header.Children.Add(copyButton);

        DockPanel.SetDock(header, Dock.Top);
        container.Children.Add(header);

        // Code body with syntax highlighting
        var codeTextBlock = new SelectableTextBlock
        {
            FontFamily = MonoFont,
            FontSize = 13,
            Margin = new Thickness(12, 4, 12, 10),
        };

        var tokens = Highlighter.Value.Tokenize(segment.Code, segment.Language);
        foreach (var token in tokens)
        {
            var run = new Run(token.Text)
            {
                Foreground = new SolidColorBrush(token.Foreground),
            };
            if (token.IsBold) run.FontWeight = FontWeight.Bold;
            if (token.IsItalic) run.FontStyle = FontStyle.Italic;
            codeTextBlock.Inlines!.Add(run);
        }

        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = codeTextBlock,
        };

        container.Children.Add(scrollViewer);
        outerBorder.Child = container;
        return outerBorder;
    }
}
