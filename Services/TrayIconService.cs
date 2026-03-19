using System;
using System.Drawing;
using System.Windows.Forms;

namespace QuickPrompt.Services;

public class TrayIconService : IDisposable
{
    private readonly Action _onToggleRequested;
    private readonly Action _onExitRequested;
    private readonly Action _onSettingsRequested;
    private NotifyIcon? _notifyIcon;

    public TrayIconService(Action onToggleRequested, Action onExitRequested, Action onSettingsRequested)
    {
        _onToggleRequested = onToggleRequested;
        _onExitRequested = onExitRequested;
        _onSettingsRequested = onSettingsRequested;
    }

    public void Initialize()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open/Hide", null, (_, _) => _onToggleRequested());
        menu.Items.Add("Settings", null, (_, _) => _onSettingsRequested());
        menu.Items.Add("Exit", null, (_, _) => _onExitRequested());

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "QuickPrompt",
            Icon = SystemIcons.Information,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => _onToggleRequested();
    }

    public void Dispose()
    {
        if (_notifyIcon is null) return;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
