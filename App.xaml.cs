using System;
using System.Windows;
using QuickPrompt.Services;
using QuickPrompt.ViewModels;

namespace QuickPrompt;

public partial class App : Application
{
    private TrayIconService? _tray;
    private MainWindow? _mainWindow;
    private SettingsService? _settingsService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsService = new SettingsService();
        var settings = _settingsService.Load();

        var screenshotService = new ScreenshotService();
        var apiClient = new OpenAiLikeClient(settings);

        var vm = new MainWindowViewModel(settings, _settingsService, apiClient, screenshotService);
        _mainWindow = new MainWindow { DataContext = vm };
        _mainWindow.Hide();

        _tray = new TrayIconService(
            onToggleRequested: ToggleMainWindow,
            onExitRequested: ExitApplication,
            onSettingsRequested: OpenSettings);

        _tray.Initialize();
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow is null) return;

        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
            return;
        }

        _mainWindow.ShowOverlay();
    }

    private void OpenSettings()
    {
        if (_mainWindow?.DataContext is not MainWindowViewModel vm)
            return;

        var window = new SettingsWindow
        {
            Owner = _mainWindow,
            DataContext = new SettingsViewModel(vm)
        };

        window.ShowDialog();
    }

    private void ExitApplication()
    {
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
