using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace Peeklism.App.Previews;

/// <summary>
/// Renders the subset of Markdown that actually appears in the files people preview:
/// headings, paragraphs, lists, block quotes, fenced code, rules, and inline emphasis,
/// code and links.
/// </summary>
/// <remarks>
/// Written by hand rather than taken from a library because a preview has to appear within
/// a frame or two of the space bar, and because anything it cannot parse must still show as
/// plain readable text instead of failing.
/// </remarks>
internal static class MarkdownRenderer
{
    private const double BodyFontSize = 14;

    public static FrameworkElement Render(string markdown)
    {
        var root = new StackPanel { Margin = new Thickness(22, 16, 22, 22), Spacing = 10 };
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var paragraph = new List<string>();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                Flush(root, paragraph);
                var code = new List<string>();
                index++;
                while (index < lines.Length && !lines[index].StartsWith("```", StringComparison.Ordinal))
                {
                    code.Add(lines[index]);
                    index++;
                }

                root.Children.Add(CodeBlock(string.Join(Environment.NewLine, code)));
                continue;
            }

            if (line.Trim().Length == 0)
            {
                Flush(root, paragraph);
                continue;
            }

            if (IsHorizontalRule(line))
            {
                Flush(root, paragraph);
                root.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, 6, 0, 6),
                    Background = Resource("DividerStrokeColorDefaultBrush"),
                });
                continue;
            }

            var heading = HeadingLevel(line);
            if (heading > 0)
            {
                Flush(root, paragraph);
                root.Children.Add(Heading(line[heading..].Trim(), heading));
                continue;
            }

            if (line.StartsWith(">", StringComparison.Ordinal))
            {
                Flush(root, paragraph);
                root.Children.Add(Quote(line.TrimStart('>').Trim()));
                continue;
            }

            if (line.Contains('|', StringComparison.Ordinal)
                && index + 1 < lines.Length
                && IsTableSeparator(lines[index + 1]))
            {
                Flush(root, paragraph);
                var rows = new List<string[]> { SplitRow(line) };
                index += 2;
                while (index < lines.Length && lines[index].Contains('|', StringComparison.Ordinal))
                {
                    rows.Add(SplitRow(lines[index]));
                    index++;
                }

                // The outer loop advances again; step back onto the last consumed line.
                index--;
                root.Children.Add(Table(rows));
                continue;
            }

            if (TryReadListMarker(line, out var marker, out var content))
            {
                Flush(root, paragraph);
                root.Children.Add(ListItem(marker, content));
                continue;
            }

            paragraph.Add(line.Trim());
        }

        Flush(root, paragraph);
        return root;
    }

    private static void Flush(Panel root, List<string> paragraph)
    {
        if (paragraph.Count == 0)
        {
            return;
        }

        root.Children.Add(Body(string.Join(" ", paragraph)));
        paragraph.Clear();
    }

    private static int HeadingLevel(string line)
    {
        var level = 0;
        while (level < line.Length && line[level] == '#')
        {
            level++;
        }

        // A run of hashes is only a heading when a space follows it.
        return level is > 0 and <= 6 && level < line.Length && line[level] == ' ' ? level : 0;
    }

    private static bool IsHorizontalRule(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 3
            && (trimmed.All(character => character == '-')
                || trimmed.All(character => character == '*')
                || trimmed.All(character => character == '_'));
    }

    private static bool TryReadListMarker(string line, out string marker, out string content)
    {
        marker = string.Empty;
        content = string.Empty;
        var trimmed = line.TrimStart();
        if (trimmed.Length > 2
            && trimmed[0] is '-' or '*' or '+'
            && trimmed[1] == ' ')
        {
            marker = "•";
            content = trimmed[2..];
            return true;
        }

        var digits = 0;
        while (digits < trimmed.Length && char.IsAsciiDigit(trimmed[digits]))
        {
            digits++;
        }

        if (digits > 0 && digits + 1 < trimmed.Length && trimmed[digits] == '.' && trimmed[digits + 1] == ' ')
        {
            marker = trimmed[..digits] + ".";
            content = trimmed[(digits + 2)..];
            return true;
        }

        return false;
    }

    private static FrameworkElement Heading(string text, int level)
    {
        var block = new TextBlock
        {
            FontSize = level switch { 1 => 24, 2 => 19, 3 => 16.5, _ => BodyFontSize + 1 },
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, level <= 2 ? 10 : 6, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };
        AddInlines(block, text);
        return block;
    }

    private static FrameworkElement Body(string text)
    {
        var block = new TextBlock
        {
            FontSize = BodyFontSize,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            LineHeight = BodyFontSize * 1.55,
        };
        AddInlines(block, text);
        return block;
    }

    private static FrameworkElement ListItem(string marker, string text)
    {
        var row = new Grid { ColumnSpacing = 10, Margin = new Thickness(6, 0, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var bullet = new TextBlock
        {
            Text = marker,
            FontSize = BodyFontSize,
            Foreground = Resource("TextFillColorSecondaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        row.Children.Add(bullet);
        var content = Body(text);
        Grid.SetColumn(content, 1);
        row.Children.Add(content);
        return row;
    }

    /// <summary>
    /// A GFM separator row: only pipes, dashes, alignment colons and spaces.
    /// </summary>
    private static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Contains('-', StringComparison.Ordinal)
            && trimmed.Contains('|', StringComparison.Ordinal)
            && trimmed.All(character => character is '|' or '-' or ':' or ' ');
    }

    private static string[] SplitRow(string line) =>
        line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();

    private static FrameworkElement Table(List<string[]> rows)
    {
        var columns = rows.Max(row => row.Length);
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        for (var column = 0; column < columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        for (var row = 0; row < rows.Count; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var column = 0; column < columns; column++)
            {
                var text = column < rows[row].Length ? rows[row][column] : string.Empty;
                var cell = Body(text);
                cell.Margin = new Thickness(0);
                if (row == 0 && cell is TextBlock header)
                {
                    header.FontWeight = FontWeights.SemiBold;
                }

                var container = new Border
                {
                    Padding = new Thickness(12, 7, 12, 7),
                    BorderBrush = Resource("DividerStrokeColorDefaultBrush"),

                    // A single hairline under every row, and one more under the header.
                    BorderThickness = new Thickness(0, 0, 0, row == 0 ? 1.5 : 1),
                    Child = cell,
                };
                Grid.SetRow(container, row);
                Grid.SetColumn(container, column);
                grid.Children.Add(container);
            }
        }

        return new ScrollViewer
        {
            Content = grid,
            HorizontalScrollMode = ScrollMode.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
        };
    }

    private static FrameworkElement Quote(string text)
    {
        var content = Body(text);
        content.Margin = new Thickness(14, 2, 0, 2);
        if (content is TextBlock block)
        {
            block.Foreground = Resource("TextFillColorSecondaryBrush");
        }

        return new Border
        {
            BorderBrush = Resource("AccentFillColorDefaultBrush"),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Child = content,
        };
    }

    private static FrameworkElement CodeBlock(string code) => new Border
    {
        Background = Resource("SubtleFillColorSecondaryBrush"),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(14, 10, 14, 10),
        Child = new ScrollViewer
        {
            HorizontalScrollMode = ScrollMode.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
            Content = new TextBlock
            {
                Text = code,
                FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                FontSize = 12.5,
                TextWrapping = TextWrapping.NoWrap,
                IsTextSelectionEnabled = true,
            },
        },
    };

    /// <summary>
    /// Splits one line into runs. Emphasis markers are matched only when a closing marker
    /// exists on the same line, so stray asterisks stay visible rather than swallowing the
    /// rest of the text.
    /// </summary>
    private static void AddInlines(TextBlock block, string text)
    {
        var position = 0;
        var literal = new System.Text.StringBuilder();

        void FlushLiteral()
        {
            if (literal.Length == 0)
            {
                return;
            }

            block.Inlines.Add(new Run { Text = literal.ToString() });
            literal.Clear();
        }

        while (position < text.Length)
        {
            var character = text[position];

            if (character == '`' && TryMatch(text, position, "`", out var codeEnd))
            {
                FlushLiteral();
                block.Inlines.Add(new Run
                {
                    Text = text[(position + 1)..codeEnd],
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                });
                position = codeEnd + 1;
                continue;
            }

            if (character == '*' && position + 1 < text.Length && text[position + 1] == '*'
                && TryMatch(text, position + 1, "**", out var boldEnd))
            {
                FlushLiteral();
                block.Inlines.Add(new Run
                {
                    Text = text[(position + 2)..boldEnd],
                    FontWeight = FontWeights.SemiBold,
                });
                position = boldEnd + 2;
                continue;
            }

            if (character is '*' or '_' && TryMatch(text, position, character.ToString(), out var italicEnd))
            {
                FlushLiteral();
                block.Inlines.Add(new Run
                {
                    Text = text[(position + 1)..italicEnd],
                    FontStyle = Windows.UI.Text.FontStyle.Italic,
                });
                position = italicEnd + 1;
                continue;
            }

            if (character == '[' && TryReadLink(text, position, out var label, out var length))
            {
                FlushLiteral();
                block.Inlines.Add(new Run
                {
                    Text = label,
                    Foreground = Resource("AccentTextFillColorPrimaryBrush"),
                });
                position += length;
                continue;
            }

            literal.Append(character);
            position++;
        }

        FlushLiteral();
    }

    private static bool TryMatch(string text, int start, string marker, out int end)
    {
        end = text.IndexOf(marker, start + marker.Length, StringComparison.Ordinal);
        return end > start + marker.Length - 1 && end > start;
    }

    private static bool TryReadLink(string text, int start, out string label, out int length)
    {
        label = string.Empty;
        length = 0;
        var labelEnd = text.IndexOf(']', start);
        if (labelEnd < 0 || labelEnd + 1 >= text.Length || text[labelEnd + 1] != '(')
        {
            return false;
        }

        var target = text.IndexOf(')', labelEnd);
        if (target < 0)
        {
            return false;
        }

        label = text[(start + 1)..labelEnd];
        length = target - start + 1;
        return true;
    }

    private static Brush Resource(string key) => (Brush)Application.Current.Resources[key];
}
