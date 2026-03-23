using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using QuickPrompt.Services;
using QuickPrompt.ViewModels;

namespace QuickPrompt;

public partial class MainWindow : Window
{
    private readonly HotkeyService _hotkeyService = new();
    private readonly DispatcherTimer _idleTimer;
    private INotifyCollectionChanged? _messagesSource;
    private MainWindowViewModel? _vm;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _idleTimer.Tick += OnIdleTimerTick;

        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        Closing += OnWindowClosing;
        IsVisibleChanged += OnIsVisibleChanged;
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
                return;
            }

            ShowOverlay();
        });
    }

    public void ShowOverlay()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        PlaceWindowNearBottomCenter();

        Show();
        Activate();
        Topmost = true;
        Focus();
        PromptTextBox.Focus();

        _vm?.ExpandFromUserActivity();
        AnimateToVmSize(immediate: true);
        RestartIdleTimer();
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

        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = e.NewValue as MainWindowViewModel;

        if (_vm is null)
        {
            return;
        }

        _messagesSource = _vm.Messages;
        _messagesSource.CollectionChanged += OnMessagesCollectionChanged;
        _vm.PropertyChanged += OnVmPropertyChanged;
        AnimateToVmSize(immediate: true);
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.TargetOverlayHeight) or nameof(MainWindowViewModel.TargetOverlayWidth))
        {
            AnimateToVmSize();
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
        _vm?.ResetSession();
        Hide();
    }

    private void PromptTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        RestartIdleTimer();

        if (e.Key != Key.Enter || Keyboard.Modifiers == ModifierKeys.Shift)
        {
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        if (_vm is not null && _vm.SendCommand.CanExecute(null))
        {
            _vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void PromptTextBox_OnGotFocus(object sender, RoutedEventArgs e)
    {
        _vm?.ExpandFromUserActivity();
        AnimateToVmSize();
        RestartIdleTimer();
    }

    private void PromptTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        RestartIdleTimer();
    }

    private void Root_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        RestartIdleTimer();

        if (e.ClickCount == 2)
        {
            _vm?.ExpandFromUserActivity();
            PromptTextBox.Focus();
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Root_OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _vm?.ExpandFromUserActivity();
        RestartIdleTimer();
    }

    private void OnIdleTimerTick(object? sender, EventArgs e)
    {
        _idleTimer.Stop();
        _vm?.CollapseIfIdle();
        AnimateToVmSize();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            RestartIdleTimer();
            return;
        }

        _idleTimer.Stop();
    }

    private void RestartIdleTimer()
    {
        _idleTimer.Stop();
        _idleTimer.Start();
    }

    private void AnimateToVmSize(bool immediate = false)
    {
        if (_vm is null)
        {
            return;
        }

        var targetHeight = _vm.TargetOverlayHeight;
        var targetWidth = _vm.TargetOverlayWidth;

        var (targetLeft, targetTop) = ComputeTargetPlacement(targetWidth, targetHeight);

        if (immediate)
        {
            Height = targetHeight;
            Width = targetWidth;
            Left = targetLeft;
            Top = targetTop;
            return;
        }

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginAnimation(HeightProperty, new DoubleAnimation(targetHeight, TimeSpan.FromMilliseconds(250)) { EasingFunction = easing });
        BeginAnimation(WidthProperty, new DoubleAnimation(targetWidth, TimeSpan.FromMilliseconds(250)) { EasingFunction = easing });
        BeginAnimation(LeftProperty, new DoubleAnimation(targetLeft, TimeSpan.FromMilliseconds(250)) { EasingFunction = easing });
        BeginAnimation(TopProperty, new DoubleAnimation(targetTop, TimeSpan.FromMilliseconds(250)) { EasingFunction = easing });
    }

    private void PlaceWindowNearBottomCenter()
    {
        var (left, top) = ComputeTargetPlacement(Width, Height);
        Left = left;
        Top = top;
    }

    private static (double Left, double Top) ComputeTargetPlacement(double width, double height)
    {
        var workArea = SystemParameters.WorkArea;
        var left = workArea.Left + ((workArea.Width - width) / 2);
        var top = workArea.Top + ((workArea.Height - height) / 2);
        return (left, top);
    }

    private void HideButton_OnClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_messagesSource is not null)
        {
            _messagesSource.CollectionChanged -= OnMessagesCollectionChanged;
        }

        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _idleTimer.Stop();
        _hotkeyService.Dispose();
        base.OnClosed(e);
    }
}
