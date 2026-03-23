using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using QuickPrompt.Services;
using QuickPrompt.ViewModels;

namespace QuickPrompt;

public partial class MainWindow : Window
{
    private readonly HotkeyService _hotkeyService = new();
    private INotifyCollectionChanged? _messagesSource;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        Closing += OnWindowClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        _hotkeyService.Register(helper.Handle, HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x20, ToggleVisibility);
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

    public void PrepareForExit()
    {
        _allowClose = true;
        Close();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_messagesSource is not null)
        {
            _messagesSource.CollectionChanged -= OnMessagesCollectionChanged;
            _messagesSource = null;
        }

        if (e.NewValue is MainWindowViewModel vm)
        {
            _messagesSource = vm.Messages;
            _messagesSource.CollectionChanged += OnMessagesCollectionChanged;
        }
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (ChatList.Items.Count > 0)
            {
                ChatList.ScrollIntoView(ChatList.Items[ChatList.Items.Count - 1]);
            }
        });
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;

        if (DataContext is MainWindowViewModel vm)
        {
            vm.ResetSession();
        }

        Hide();
    }

    private void PromptTextBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers == ModifierKeys.Shift)
        {
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        if (DataContext is MainWindowViewModel vm && vm.SendCommand.CanExecute(null))
        {
            vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void HideButton_OnClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_messagesSource is not null)
        {
            _messagesSource.CollectionChanged -= OnMessagesCollectionChanged;
        }

        _hotkeyService.Dispose();
        base.OnClosed(e);
    }
}
