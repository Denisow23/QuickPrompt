using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace QuickPrompt.Services;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

public class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 9000;

    private HwndSource? _source;
    private HwndSourceHook? _hook;

    public void Register(IntPtr handle, HotkeyModifiers modifiers, int virtualKey, Action callback)
    {
        _source = HwndSource.FromHwnd(handle);
        _hook = (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (msg != WmHotkey || wParam.ToInt32() != HotkeyId)
            {
                return IntPtr.Zero;
            }

            callback();
            handled = true;
            return IntPtr.Zero;
        };

        _source?.AddHook(_hook);

        RegisterHotKey(handle, HotkeyId, (uint)modifiers, (uint)virtualKey);
    }

    public void Dispose()
    {
        if (_source is null) return;
        if (_hook is not null)
        {
            _source.RemoveHook(_hook);
        }

        UnregisterHotKey(_source.Handle, HotkeyId);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
