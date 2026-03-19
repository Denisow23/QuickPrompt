using System;
using System.Windows;
using System.Windows.Interop;
using QuickPrompt.Services;

namespace QuickPrompt;

public partial class MainWindow : Window
{
    private readonly HotkeyService _hotkeyService = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        _hotkeyService.Register(helper.Handle, HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x20, ToggleVisibility); // Ctrl+Shift+Space
    }

    private void ToggleVisibility()
    {
        Dispatcher.Invoke(() =>
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                ShowOverlay();
            }
        });
    }

    public void ShowOverlay()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = SystemParameters.WorkArea.Right - Width - 16;
        Top = SystemParameters.WorkArea.Bottom - Height - 16;

        Show();
        Activate();
        Topmost = true;
        Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotkeyService.Dispose();
        base.OnClosed(e);
    }
}
