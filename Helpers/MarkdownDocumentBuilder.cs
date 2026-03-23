using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using FlowList = System.Windows.Documents.List;

namespace QuickPrompt.Helpers;

public static class MarkdownDocumentBuilder
{
    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new(@"^\s*\d+\.\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex UnorderedListRegex = new(@"^\s*[-*+]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex QuoteRegex = new(@"^\s*>\s?(.*)$", RegexOptions.Compiled);
    private static readonly Regex InlineRegex = new(
        @"(\[([^\]]+)\]\((https?:\/\/[^\s)]+)\))|(`([^`]+)`)|(\*\*([^*]+)\*\*)|(__([^_]+)__)|(\*([^*\n]+)\*)|(_([^_\n]+)_)|(~~([^~]+)~~)",
        RegexOptions.Compiled);

    public static FlowDocument Build(string markdown, bool isUserMessage)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 15,
            Foreground = BrushFromHex(isUserMessage ? "#F8FCFF" : "#E9EEF7"),
            Background = System.Windows.Media.Brushes.Transparent,
            TextAlignment = TextAlignment.Left
        };

        if (string.IsNullOrWhiteSpace(markdown))
        {
            document.Blocks.Add(new Paragraph(new Run(" ")) { Margin = new Thickness(0) });
            return document;
        }

        var normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var paragraphBuffer = new List<string>();
        var codeBuffer = new List<string>();
        var inCodeFence = false;
        string? codeLanguage = null;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];

            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph(document, paragraphBuffer, isUserMessage);

                if (inCodeFence)
                {
                    document.Blocks.Add(CreateCodeBlock(codeBuffer, codeLanguage, isUserMessage));
                    codeBuffer.Clear();
                    codeLanguage = null;
                    inCodeFence = false;
                }
                else
                {
                    codeLanguage = line.Trim().Length > 3 ? line.Trim()[3..].Trim() : null;
                    inCodeFence = true;
                }

                continue;
            }

            if (inCodeFence)
            {
                codeBuffer.Add(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(document, paragraphBuffer, isUserMessage);
                continue;
            }

            if (TryCreateHeading(line, isUserMessage, out var heading))
            {
                FlushParagraph(document, paragraphBuffer, isUserMessage);
                document.Blocks.Add(heading);
                continue;
            }

            if (IsHorizontalRule(line))
            {
                FlushParagraph(document, paragraphBuffer, isUserMessage);
                document.Blocks.Add(CreateRuleBlock());
                continue;
            }

            if (TryCreateList(lines, ref index, isUserMessage, out var listBlock))
            {
                FlushParagraph(document, paragraphBuffer, isUserMessage);
                document.Blocks.Add(listBlock);
                continue;
            }

            if (TryCreateQuote(lines, ref index, isUserMessage, out var quoteBlock))
            {
                FlushParagraph(document, paragraphBuffer, isUserMessage);
                document.Blocks.Add(quoteBlock);
                continue;
            }

            paragraphBuffer.Add(line.Trim());
        }

        if (inCodeFence)
        {
            document.Blocks.Add(CreateCodeBlock(codeBuffer, codeLanguage, isUserMessage));
        }
        else
        {
            FlushParagraph(document, paragraphBuffer, isUserMessage);
        }

        return document;
    }

    private static void FlushParagraph(FlowDocument document, List<string> paragraphBuffer, bool isUserMessage)
    {
        if (paragraphBuffer.Count == 0)
        {
            return;
        }

        document.Blocks.Add(CreateParagraph(string.Join(" ", paragraphBuffer), isUserMessage));
        paragraphBuffer.Clear();
    }

    private static Paragraph CreateParagraph(string text, bool isUserMessage)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 10),
            LineHeight = 24
        };

        AppendInlines(paragraph.Inlines, text, isUserMessage);
        return paragraph;
    }

    private static bool TryCreateHeading(string line, bool isUserMessage, out Paragraph heading)
    {
        var match = HeadingRegex.Match(line);
        if (!match.Success)
        {
            heading = null!;
            return false;
        }

        var level = match.Groups[1].Value.Length;
        heading = new Paragraph
        {
            Margin = new Thickness(0, 2, 0, 10),
            FontWeight = FontWeights.SemiBold,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Display, Segoe UI"),
            FontSize = level switch
            {
                1 => 24,
                2 => 21,
                3 => 19,
                _ => 17
            }
        };

        AppendInlines(heading.Inlines, match.Groups[2].Value.Trim(), isUserMessage);
        return true;
    }

    private static bool TryCreateList(string[] lines, ref int index, bool isUserMessage, out FlowList listBlock)
    {
        listBlock = null!;
        var line = lines[index];

        var isOrdered = OrderedListRegex.IsMatch(line);
        var isUnordered = UnorderedListRegex.IsMatch(line);
        if (!isOrdered && !isUnordered)
        {
            return false;
        }

        listBlock = new FlowList
        {
            Margin = new Thickness(0, 2, 0, 12),
            Padding = new Thickness(10, 0, 0, 0),
            MarkerStyle = isOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc
        };

        while (index < lines.Length)
        {
            var current = lines[index];
            var match = isOrdered ? OrderedListRegex.Match(current) : UnorderedListRegex.Match(current);
            if (!match.Success)
            {
                index--;
                return true;
            }

            var paragraph = CreateParagraph(match.Groups[1].Value.Trim(), isUserMessage);
            paragraph.Margin = new Thickness(0, 0, 0, 6);
            listBlock.ListItems.Add(new ListItem(paragraph));
            index++;
        }

        index--;
        return true;
    }

    private static bool TryCreateQuote(string[] lines, ref int index, bool isUserMessage, out Paragraph quoteBlock)
    {
        quoteBlock = null!;
        var match = QuoteRegex.Match(lines[index]);
        if (!match.Success)
        {
            return false;
        }

        var quoteLines = new List<string>();
        while (index < lines.Length)
        {
            var currentMatch = QuoteRegex.Match(lines[index]);
            if (!currentMatch.Success)
            {
                index--;
                break;
            }

            quoteLines.Add(currentMatch.Groups[1].Value);
            index++;
        }

        quoteBlock = CreateParagraph(string.Join(" ", quoteLines), isUserMessage);
        quoteBlock.Margin = new Thickness(0, 2, 0, 12);
        quoteBlock.Padding = new Thickness(14, 10, 0, 10);
        quoteBlock.BorderThickness = new Thickness(3, 0, 0, 0);
        quoteBlock.BorderBrush = BrushFromHex(isUserMessage ? "#70FFFFFF" : "#69A2FF");
        quoteBlock.Background = BrushFromHex(isUserMessage ? "#16FFFFFF" : "#140F172A");
        return true;
    }

    private static Paragraph CreateCodeBlock(List<string> codeLines, string? language, bool isUserMessage)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 4, 0, 14),
            Padding = new Thickness(14, 12, 14, 12),
            Background = BrushFromHex(isUserMessage ? "#1FFFFFFF" : "#FF0F172A"),
            BorderBrush = BrushFromHex(isUserMessage ? "#35FFFFFF" : "#1E8BA7C4"),
            BorderThickness = new Thickness(1),
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 13.5,
            LineHeight = 20
        };

        if (!string.IsNullOrWhiteSpace(language))
        {
            paragraph.Inlines.Add(new Run($"{language.Trim()}\n")
            {
                Foreground = BrushFromHex(isUserMessage ? "#B8F8FCFF" : "#9EC8FF"),
                FontWeight = FontWeights.SemiBold
            });
        }

        var code = string.Join("\n", codeLines);
        var split = code.Split('\n');
        for (var i = 0; i < split.Length; i++)
        {
            paragraph.Inlines.Add(new Run(split[i]));
            if (i < split.Length - 1)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
        }

        return paragraph;
    }

    private static BlockUIContainer CreateRuleBlock()
    {
        var line = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 8, 0, 16),
            Background = BrushFromHex("#24FFFFFF")
        };

        return new BlockUIContainer(line);
    }

    private static bool IsHorizontalRule(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 3 &&
               (trimmed.Replace("-", string.Empty).Length == 0 ||
                trimmed.Replace("*", string.Empty).Length == 0);
    }

    private static void AppendInlines(InlineCollection inlines, string text, bool isUserMessage)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var position = 0;
        foreach (Match match in InlineRegex.Matches(text))
        {
            if (match.Index > position)
            {
                inlines.Add(new Run(text[position..match.Index]));
            }

            if (match.Groups[2].Success && match.Groups[3].Success)
            {
                var hyperlink = new Hyperlink(new Run(match.Groups[2].Value))
                {
                    NavigateUri = new Uri(match.Groups[3].Value),
                    Foreground = BrushFromHex(isUserMessage ? "#FFFFFF" : "#8DD5FF"),
                    TextDecorations = TextDecorations.Underline
                };
                hyperlink.RequestNavigate += OnHyperlinkNavigate;
                inlines.Add(hyperlink);
            }
            else if (match.Groups[5].Success)
            {
                inlines.Add(CreateInlineCode(match.Groups[5].Value, isUserMessage));
            }
            else if (match.Groups[7].Success || match.Groups[9].Success)
            {
                var boldText = match.Groups[7].Success ? match.Groups[7].Value : match.Groups[9].Value;
                inlines.Add(new Bold(new Run(boldText)));
            }
            else if (match.Groups[11].Success || match.Groups[13].Success)
            {
                var italicText = match.Groups[11].Success ? match.Groups[11].Value : match.Groups[13].Value;
                inlines.Add(new Italic(new Run(italicText)));
            }
            else if (match.Groups[15].Success)
            {
                inlines.Add(new Span(new Run(match.Groups[15].Value))
                {
                    TextDecorations = TextDecorations.Strikethrough
                });
            }

            position = match.Index + match.Length;
        }

        if (position < text.Length)
        {
            inlines.Add(new Run(text[position..]));
        }
    }

    private static InlineUIContainer CreateInlineCode(string code, bool isUserMessage)
    {
        var border = new Border
        {
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(1, -1, 1, -1),
            CornerRadius = new CornerRadius(6),
            Background = BrushFromHex(isUserMessage ? "#20FFFFFF" : "#FF1A2433"),
            BorderBrush = BrushFromHex(isUserMessage ? "#2DFFFFFF" : "#1C9CB3CC"),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = code,
                FontFamily = new System.Windows.Media.FontFamily("Cascadia Code, Consolas, Courier New"),
                FontSize = 13,
                Foreground = BrushFromHex(isUserMessage ? "#F8FCFF" : "#E9EEF7")
            }
        };

        return new InlineUIContainer(border);
    }

    private static void OnHyperlinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore hyperlink navigation failures inside chat bubbles.
        }
    }

    private static SolidColorBrush BrushFromHex(string hex)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
    }
}
