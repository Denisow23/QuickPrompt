using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using QuickPrompt.Helpers;
using QuickPrompt.Models;
using QuickPrompt.Services;

namespace QuickPrompt.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainWindowViewModel;

    private string _baseUrl = string.Empty;
    private string _apiKey = string.Empty;
    private string _defaultModel = string.Empty;
    private double _temperature = 0.2;
    private int _maxTokens = 1000;
    private string _additionalHeadersJson = "{}";
    private string _activationMode = "CtrlShiftSpace";
    private string _hotkeyGesture = "Ctrl+Shift+Space";

    public SettingsViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;

        var settingsService = new SettingsService();
        var settings = settingsService.Load();

        BaseUrl = settings.BaseUrl;
        ApiKey = settingsService.Unprotect(settings.EncryptedApiKey);
        DefaultModel = settings.DefaultModel;
        Temperature = settings.Temperature;
        MaxTokens = settings.MaxTokens;
        AdditionalHeadersJson = JsonSerializer.Serialize(settings.AdditionalHeaders, new JsonSerializerOptions { WriteIndented = true });
        ActivationMode = string.IsNullOrWhiteSpace(settings.ActivationMode) ? "CtrlShiftSpace" : settings.ActivationMode;
        HotkeyGesture = string.IsNullOrWhiteSpace(settings.HotkeyGesture)
            ? BuildGesture(settings.HotkeyModifiers, settings.HotkeyVirtualKey)
            : settings.HotkeyGesture;

        SaveCommand = new RelayCommand(Save);
    }

    public ObservableCollection<string> ActivationModes { get; } =
        new(["CtrlShiftSpace", "GlobalHotkey", "DoubleShift", "MiddleMouse"]);

    public string BaseUrl
    {
        get => _baseUrl;
        set
        {
            _baseUrl = value;
            OnPropertyChanged();
        }
    }

    public string ApiKey
    {
        get => _apiKey;
        set
        {
            _apiKey = value;
            OnPropertyChanged();
        }
    }

    public string DefaultModel
    {
        get => _defaultModel;
        set
        {
            _defaultModel = value;
            OnPropertyChanged();
        }
    }

    public double Temperature
    {
        get => _temperature;
        set
        {
            _temperature = value;
            OnPropertyChanged();
        }
    }

    public int MaxTokens
    {
        get => _maxTokens;
        set
        {
            _maxTokens = value;
            OnPropertyChanged();
        }
    }

    public string AdditionalHeadersJson
    {
        get => _additionalHeadersJson;
        set
        {
            _additionalHeadersJson = value;
            OnPropertyChanged();
        }
    }

    public string ActivationMode
    {
        get => _activationMode;
        set
        {
            _activationMode = value;
            OnPropertyChanged();
        }
    }

    public string HotkeyGesture
    {
        get => _hotkeyGesture;
        set
        {
            _hotkeyGesture = value;
            OnPropertyChanged();
        }
    }

    public RelayCommand SaveCommand { get; }

    private void Save()
    {
        try
        {
            var service = new SettingsService();
            var additionalHeaders =
                JsonSerializer.Deserialize<Dictionary<string, string>>(AdditionalHeadersJson)
                ?? new Dictionary<string, string>();

            var settings = new AppSettings
            {
                BaseUrl = BaseUrl.Trim(),
                EncryptedApiKey = service.Protect(ApiKey),
                DefaultModel = DefaultModel.Trim(),
                Temperature = Temperature,
                MaxTokens = MaxTokens,
                AdditionalHeaders = additionalHeaders,
                ActivationMode = ActivationMode,
                HotkeyGesture = HotkeyGesture.Trim(),
                HotkeyModifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
                HotkeyVirtualKey = 0x20
            };

            if (string.Equals(ActivationMode, "GlobalHotkey", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseGesture(HotkeyGesture, out var modifiers, out var key, out var error))
                {
                    throw new InvalidOperationException(error);
                }

                settings.HotkeyModifiers = modifiers;
                settings.HotkeyVirtualKey = key;
                settings.HotkeyGesture = BuildGesture(modifiers, key);
            }
            else if (string.Equals(ActivationMode, "CtrlShiftSpace", StringComparison.OrdinalIgnoreCase))
            {
                settings.HotkeyModifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift;
                settings.HotkeyVirtualKey = 0x20;
                settings.HotkeyGesture = "Ctrl+Shift+Space";
            }

            service.Save(settings);
            _mainWindowViewModel.ApplySettings(settings);
            System.Windows.MessageBox.Show("Настройки сохранены.", "QuickPrompt", MessageBoxButton.OK, MessageBoxImage.Information);

            if (System.Windows.Application.Current.Windows.Count <= 0)
            {
                return;
            }

            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is SettingsWindow)
                {
                    window.Close();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Не удалось сохранить настройки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool TryParseGesture(string? input, out HotkeyModifiers modifiers, out int keyCode, out string error)
    {
        modifiers = HotkeyModifiers.None;
        keyCode = 0;
        error = "Не удалось распознать хоткей.";

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Введите комбинацию в формате Ctrl+Alt+K.";
            return false;
        }

        var tokens = input.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            switch (token.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= HotkeyModifiers.Control;
                    break;
                case "shift":
                    modifiers |= HotkeyModifiers.Shift;
                    break;
                case "alt":
                    modifiers |= HotkeyModifiers.Alt;
                    break;
                case "win":
                case "windows":
                    modifiers |= HotkeyModifiers.Win;
                    break;
                default:
                    if (!TryParsePrimaryKey(token, out keyCode))
                    {
                        error = $"Неизвестная клавиша: {token}";
                        return false;
                    }
                    break;
            }
        }

        if (keyCode == 0)
        {
            error = "Нужна основная клавиша (например: Ctrl+Shift+K).";
            return false;
        }

        if (modifiers == HotkeyModifiers.None)
        {
            error = "Добавьте хотя бы один модификатор: Ctrl / Alt / Shift / Win.";
            return false;
        }

        return true;
    }

    private static bool TryParsePrimaryKey(string token, out int keyCode)
    {
        keyCode = 0;

        if (token.Length == 1 && char.IsLetterOrDigit(token[0]))
        {
            keyCode = char.ToUpperInvariant(token[0]);
            return true;
        }

        if (token.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(token[1..], out var fKey) &&
            fKey is >= 1 and <= 24)
        {
            keyCode = 0x70 + (fKey - 1);
            return true;
        }

        return token.ToLowerInvariant() switch
        {
            "space" => Set(out keyCode, 0x20),
            "enter" => Set(out keyCode, 0x0D),
            "tab" => Set(out keyCode, 0x09),
            "esc" or "escape" => Set(out keyCode, 0x1B),
            "backspace" => Set(out keyCode, 0x08),
            "delete" or "del" => Set(out keyCode, 0x2E),
            "insert" or "ins" => Set(out keyCode, 0x2D),
            "home" => Set(out keyCode, 0x24),
            "end" => Set(out keyCode, 0x23),
            "pageup" or "pgup" => Set(out keyCode, 0x21),
            "pagedown" or "pgdown" => Set(out keyCode, 0x22),
            "left" => Set(out keyCode, 0x25),
            "right" => Set(out keyCode, 0x27),
            "up" => Set(out keyCode, 0x26),
            "down" => Set(out keyCode, 0x28),
            _ => false
        };
    }

    private static bool Set(out int keyCode, int value)
    {
        keyCode = value;
        return true;
    }

    private static string BuildGesture(HotkeyModifiers modifiers, int keyCode)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");

        parts.Add(keyCode switch
        {
            0x20 => "Space",
            >= 0x70 and <= 0x87 => $"F{keyCode - 0x6F}",
            _ => ((char)keyCode).ToString()
        });

        return string.Join("+", parts);
    }
}
