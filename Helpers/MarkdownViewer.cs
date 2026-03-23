using System.Windows;
using System.Windows.Media;

namespace QuickPrompt.Helpers;

public class MarkdownViewer : System.Windows.Controls.RichTextBox
{
    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(MarkdownViewer),
            new PropertyMetadata(string.Empty, OnMarkdownPropertyChanged));

    public static readonly DependencyProperty IsUserMessageProperty =
        DependencyProperty.Register(
            nameof(IsUserMessage),
            typeof(bool),
            typeof(MarkdownViewer),
            new PropertyMetadata(false, OnMarkdownPropertyChanged));

    public MarkdownViewer()
    {
        IsReadOnly = true;
        IsDocumentEnabled = true;
        BorderThickness = new Thickness(0);
        Background = System.Windows.Media.Brushes.Transparent;
        Padding = new Thickness(0);
        Margin = new Thickness(0);
        Focusable = false;
        VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
        HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
    }

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public bool IsUserMessage
    {
        get => (bool)GetValue(IsUserMessageProperty);
        set => SetValue(IsUserMessageProperty, value);
    }

    private static void OnMarkdownPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            viewer.Document = MarkdownDocumentBuilder.Build(viewer.Markdown, viewer.IsUserMessage);
        }
    }
}
