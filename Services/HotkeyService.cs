using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using QuickPrompt.Models;

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

public enum ActivationMode
{
    CtrlShiftSpace,
    GlobalHotkey,
    DoubleShift,
    MiddleMouse
}

public class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 9000;

    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmMButtonDown = 0x0207;
    private const int VkShift = 0x10;

    private HwndSource? _source;
    private HwndSourceHook? _hook;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;
    private LowLevelProc? _keyboardProc;
    private LowLevelProc? _mouseProc;
    private Action? _callback;
    private DateTime _lastShiftDownUtc = DateTime.MinValue;

    public void RegisterFromSettings(IntPtr handle, AppSettings settings, Action callback)
    {
        UnregisterAll();

        _source = HwndSource.FromHwnd(handle);
        _callback = callback;

        var mode = ParseActivationMode(settings.ActivationMode);
        switch (mode)
        {
            case ActivationMode.DoubleShift:
                InstallKeyboardHook();
                break;
            case ActivationMode.MiddleMouse:
                InstallMouseHook();
                break;
            case ActivationMode.GlobalHotkey:
                RegisterGlobalHotkey(handle, settings.HotkeyModifiers, settings.HotkeyVirtualKey);
                break;
            default:
                RegisterGlobalHotkey(handle, HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x20);
                break;
        }
    }

    public void Dispose()
    {
        UnregisterAll();
    }

    private void RegisterGlobalHotkey(IntPtr handle, HotkeyModifiers modifiers, int virtualKey)
    {
        _hook = (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (msg != WmHotkey || wParam.ToInt32() != HotkeyId)
            {
                return IntPtr.Zero;
            }

            _callback?.Invoke();
            handled = true;
            return IntPtr.Zero;
        };

        _source?.AddHook(_hook);

        if (!RegisterHotKey(handle, HotkeyId, (uint)modifiers, (uint)virtualKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось зарегистрировать глобальный хоткей.");
        }
    }

    private void InstallKeyboardHook()
    {
        _keyboardProc = KeyboardHookCallback;
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, IntPtr.Zero, 0);
        if (_keyboardHook == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось установить keyboard hook.");
        }
    }

    private void InstallMouseHook()
    {
        _mouseProc = MouseHookCallback;
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, IntPtr.Zero, 0);
        if (_mouseHook == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось установить mouse hook.");
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam.ToInt32() == WmKeyDown || wParam.ToInt32() == WmSysKeyDown))
        {
            var kb = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            if (kb.vkCode == VkShift)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastShiftDownUtc).TotalMilliseconds <= 400)
                {
                    if (_callback is not null)
                    {
                        _source?.Dispatcher.BeginInvoke(_callback);
                    }
                    _lastShiftDownUtc = DateTime.MinValue;
                }
                else
                {
                    _lastShiftDownUtc = now;
                }
            }
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam.ToInt32() == WmMButtonDown)
        {
            if (_callback is not null)
            {
                _source?.Dispatcher.BeginInvoke(_callback);
            }
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void UnregisterAll()
    {
        if (_source is not null && _hook is not null)
        {
            _source.RemoveHook(_hook);
            UnregisterHotKey(_source.Handle, HotkeyId);
            _hook = null;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
            _keyboardProc = null;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
            _mouseProc = null;
        }
    }

    private static ActivationMode ParseActivationMode(string? value)
    {
        if (Enum.TryParse<ActivationMode>(value, ignoreCase: true, out var mode))
        {
            return mode;
        }

        return ActivationMode.CtrlShiftSpace;
    }

    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
}
