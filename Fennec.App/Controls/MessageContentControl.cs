using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Fennec.App.Embeds;
using Fennec.App.Embeds.Providers;
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
    private static readonly Lazy<EmbedProviderFactory> EmbedFactory = new(() => new EmbedProviderFactory(
    [
        new YouTubeEmbedProvider(),
        new SpotifyEmbedProvider(),
        new ImageEmbedProvider(),
    ]));

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
        var embedUrls = new List<Uri>();

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

                // Collect URLs from LinkSegments (not suppressed) for embeds
                if (segment is LinkSegment link)
                    embedUrls.Add(link.Url);
            }
        }

        FlushInlines(inlineBuffer, panel);

        // Add embeds for detected links
        foreach (var url in embedUrls)
        {
            var embed = EmbedFactory.Value.TryCreateEmbed(url);
            if (embed is not null)
                panel.Children.Add(BuildEmbed(embed));
        }

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
                case LinkSegment link:
                    tb.Inlines!.Add(BuildLinkRun(link.Text, link.Url));
                    break;
                case SuppressedLinkSegment suppressed:
                    tb.Inlines!.Add(BuildLinkRun(suppressed.Text, suppressed.Url));
                    break;
            }
        }

        panel.Children.Add(tb);
        buffer.Clear();
    }

    private static Inline BuildLinkRun(string text, Uri url)
    {
        var linkText = new TextBlock
        {
            FontSize = 15,
            FontFamily = ContentFont,
            Foreground = new SolidColorBrush(Color.FromRgb(88, 166, 255)),
            TextDecorations = TextDecorations.Underline,
            Cursor = new Cursor(StandardCursorType.Hand),
            Text = text,
        };
        linkText.PointerPressed += (_, _) =>
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(url.AbsoluteUri)
                {
                    UseShellExecute = true,
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch
            {
                // Ignore if browser fails to open
            }
        };

        return new InlineUIContainer(linkText);
    }

    private static Control BuildEmbed(EmbedInfo embed)
    {
        return embed switch
        {
            YouTubeEmbed yt => BuildYouTubeEmbed(yt),
            SpotifyEmbed sp => BuildSpotifyEmbed(sp),
            ImageEmbed img => BuildImageEmbed(img),
            _ => new Panel(),
        };
    }

    private static Control BuildYouTubeEmbed(YouTubeEmbed embed)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 4),
            BorderThickness = new Thickness(1),
            MaxWidth = 400,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        BindBrushToResource(border, Border.BackgroundProperty, "CodeBlockBackgroundBrush");
        BindBrushToResource(border, Border.BorderBrushProperty, "CodeBlockBorderBrush");

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock
        {
            Text = "YouTube",
            FontSize = 11,
            Opacity = 0.6,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0)),
        });

        var thumbnail = new Avalonia.Controls.Image
        {
            MaxWidth = 380,
            MaxHeight = 214,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsVisible = false,
        };
        stack.Children.Add(thumbnail);

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        body.Children.Add(new MaterialIcon
        {
            Kind = MaterialIconKind.PlayCircleOutline,
            Width = 32,
            Height = 32,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0)),
        });

        var titleText = new TextBlock
        {
            Text = "Loading...",
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 340,
        };
        body.Children.Add(titleText);

        stack.Children.Add(body);

        var authorText = new TextBlock
        {
            FontSize = 11,
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };
        stack.Children.Add(authorText);

        border.Child = stack;

        border.PointerPressed += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(embed.SourceUrl.AbsoluteUri)
                    { UseShellExecute = true });
            }
            catch { }
        };

        _ = LoadYouTubeOEmbedAsync(embed.SourceUrl, thumbnail, titleText, authorText);

        return border;
    }

    private static async Task LoadYouTubeOEmbedAsync(
        Uri sourceUrl,
        Avalonia.Controls.Image thumbnail,
        TextBlock titleText,
        TextBlock authorText)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            var oEmbedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(sourceUrl.AbsoluteUri)}&format=json";
            var json = await httpClient.GetStringAsync(oEmbedUrl);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("title", out var titleProp))
                titleText.Text = titleProp.GetString();

            if (root.TryGetProperty("author_name", out var authorProp))
            {
                authorText.Text = authorProp.GetString();
                authorText.IsVisible = true;
            }

            if (root.TryGetProperty("thumbnail_url", out var thumbProp))
            {
                var thumbUrl = thumbProp.GetString();
                if (!string.IsNullOrEmpty(thumbUrl))
                {
                    var data = await httpClient.GetByteArrayAsync(thumbUrl);
                    var stream = new System.IO.MemoryStream(data);
                    var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                    thumbnail.Source = bitmap;
                    thumbnail.IsVisible = true;
                }
            }
        }
        catch
        {
            // Fall back to showing the raw URL
            titleText.Text = sourceUrl.AbsoluteUri;
        }
    }

    private static Control BuildSpotifyEmbed(SpotifyEmbed embed)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 4),
            BorderThickness = new Thickness(1),
            MaxWidth = 400,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        BindBrushToResource(border, Border.BackgroundProperty, "CodeBlockBackgroundBrush");
        BindBrushToResource(border, Border.BorderBrushProperty, "CodeBlockBorderBrush");

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock
        {
            Text = "Spotify",
            FontSize = 11,
            Opacity = 0.6,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 215, 96)),
        });

        var body = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        body.Children.Add(new MaterialIcon
        {
            Kind = MaterialIconKind.Music,
            Width = 32,
            Height = 32,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 215, 96)),
        });

        var info = new StackPanel { Spacing = 2 };
        info.Children.Add(new TextBlock
        {
            Text = char.ToUpper(embed.ResourceType[0]) + embed.ResourceType[1..],
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        info.Children.Add(new TextBlock
        {
            Text = embed.SourceUrl.AbsoluteUri,
            FontSize = 11,
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 320,
        });
        body.Children.Add(info);

        stack.Children.Add(body);
        border.Child = stack;

        border.PointerPressed += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(embed.SourceUrl.AbsoluteUri)
                    { UseShellExecute = true });
            }
            catch { }
        };

        return border;
    }

    private static Control BuildImageEmbed(ImageEmbed embed)
    {
        var image = new Avalonia.Controls.Image
        {
            MaxWidth = 400,
            MaxHeight = 300,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4),
        };

        // Load image asynchronously from URL
        try
        {
            var bitmap = new Avalonia.Media.Imaging.Bitmap(
                new System.IO.MemoryStream()); // placeholder — actual async load below
            image.Source = bitmap;
        }
        catch
        {
            // Fall back to a link-style display
        }

        // Use an async task to load the image
        _ = LoadImageAsync(image, embed.SourceUrl);

        return image;
    }

    private static async Task LoadImageAsync(Avalonia.Controls.Image imageControl, Uri url)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            var data = await httpClient.GetByteArrayAsync(url);
            var stream = new System.IO.MemoryStream(data);
            var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
            imageControl.Source = bitmap;
        }
        catch
        {
            // If image load fails, leave empty
        }
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
