using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using QuickPrompt.Helpers;
using QuickPrompt.Models;
using QuickPrompt.Services;

namespace QuickPrompt.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly OpenAiLikeClient _apiClient;
    private readonly ScreenshotService _screenshotService;
    private readonly List<ChatMessage> _sessionMessages = new();

    private AppSettings _settings;
    private string _promptText = string.Empty;
    private string _responseText = "Готово. Введите запрос.";
    private string _selectedModel;
    private string _screenshotStatus = "Скрин не прикреплен";
    private string? _attachedScreenshotBase64;

    public MainWindowViewModel(AppSettings settings, SettingsService settingsService, OpenAiLikeClient apiClient, ScreenshotService screenshotService)
    {
        _settings = settings;
        _settingsService = settingsService;
        _apiClient = apiClient;
        _screenshotService = screenshotService;

        _selectedModel = settings.DefaultModel;
        Models.Add(settings.DefaultModel);

        SendCommand = new AsyncRelayCommand(SendAsync, () => !string.IsNullOrWhiteSpace(PromptText));
        ClearSessionCommand = new RelayCommand(ClearSession);
        CaptureScreenshotCommand = new RelayCommand(CaptureScreenshot);
        PasteClipboardCommand = new RelayCommand(PasteClipboard);
        RefreshModelsCommand = new AsyncRelayCommand(RefreshModelsAsync);
        OpenSettingsCommand = new RelayCommand(() =>
        {
            if (System.Windows.Application.Current is App app)
            {
                var method = app.GetType().GetMethod("OpenSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(app, null);
            }
        });
    }

    public ObservableCollection<string> Models { get; } = new();

    public string PromptText
    {
        get => _promptText;
        set
        {
            _promptText = value;
            OnPropertyChanged();
            (SendCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string ResponseText
    {
        get => _responseText;
        set
        {
            _responseText = value;
            OnPropertyChanged();
        }
    }

    public string SelectedModel
    {
        get => _selectedModel;
        set
        {
            _selectedModel = value;
            OnPropertyChanged();
        }
    }

    public string ScreenshotStatus
    {
        get => _screenshotStatus;
        set
        {
            _screenshotStatus = value;
            OnPropertyChanged();
        }
    }

    public RelayCommand CaptureScreenshotCommand { get; }
    public RelayCommand PasteClipboardCommand { get; }
    public RelayCommand ClearSessionCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public AsyncRelayCommand SendCommand { get; }
    public AsyncRelayCommand RefreshModelsCommand { get; }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _apiClient.UpdateSettings(settings);
        if (string.IsNullOrWhiteSpace(SelectedModel))
        {
            SelectedModel = settings.DefaultModel;
        }
    }

    private async Task SendAsync()
    {
        try
        {
            var apiKey = _settingsService.Unprotect(_settings.EncryptedApiKey);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ResponseText = "Ошибка: API ключ не задан. Откройте Settings.";
                return;
            }

            var userMessage = BuildUserMessage(PromptText);
            _sessionMessages.Add(userMessage);

            var request = new ChatCompletionRequest
            {
                Model = string.IsNullOrWhiteSpace(SelectedModel) ? _settings.DefaultModel : SelectedModel,
                Messages = new List<ChatMessage>(_sessionMessages),
                Temperature = _settings.Temperature,
                MaxTokens = _settings.MaxTokens
            };

            ResponseText = "Запрос отправлен...";
            var assistantText = await _apiClient.SendChatAsync(request, apiKey);

            _sessionMessages.Add(new ChatMessage { Role = "assistant", Content = assistantText });
            ResponseText = assistantText;

            PromptText = string.Empty;
            _attachedScreenshotBase64 = null;
            ScreenshotStatus = "Скрин не прикреплен";
        }
        catch (Exception ex)
        {
            ResponseText = $"Ошибка: {ex.Message}";
        }
    }

    private ChatMessage BuildUserMessage(string prompt)
    {
        if (string.IsNullOrWhiteSpace(_attachedScreenshotBase64))
        {
            return new ChatMessage { Role = "user", Content = prompt };
        }

        var content = new List<MessageContentPart>
        {
            new() { Type = "text", Text = prompt },
            new() { Type = "image_url", ImageUrl = new ImageUrlWrapper { Url = $"data:image/png;base64,{_attachedScreenshotBase64}" } }
        };

        return new ChatMessage { Role = "user", Content = content };
    }

    private void CaptureScreenshot()
    {
        try
        {
            _attachedScreenshotBase64 = _screenshotService.CapturePrimaryScreenAsBase64Png();
            ScreenshotStatus = $"Скрин прикреплен ({_attachedScreenshotBase64.Length / 1024} KB b64)";
        }
        catch (Exception ex)
        {
            ResponseText = $"Не удалось сделать скрин: {ex.Message}";
        }
    }

    private void PasteClipboard()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                var sb = new StringBuilder(PromptText);
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(text);
                PromptText = sb.ToString();
            }
        }
        catch (Exception ex)
        {
            ResponseText = $"Ошибка буфера обмена: {ex.Message}";
        }
    }

    private void ClearSession()
    {
        _sessionMessages.Clear();
        _attachedScreenshotBase64 = null;
        PromptText = string.Empty;
        ResponseText = "Сессия очищена.";
        ScreenshotStatus = "Скрин не прикреплен";
    }

    public async Task RefreshModelsAsync()
    {
        try
        {
            var apiKey = _settingsService.Unprotect(_settings.EncryptedApiKey);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ResponseText = "Сначала сохраните API ключ в Settings.";
                return;
            }

            var models = await _apiClient.GetModelsAsync(apiKey);
            Models.Clear();
            foreach (var model in models)
            {
                Models.Add(model);
            }

            if (Models.Count == 0)
            {
                Models.Add(_settings.DefaultModel);
            }

            if (!Models.Contains(SelectedModel))
            {
                SelectedModel = _settings.DefaultModel;
            }
        }
        catch (Exception ex)
        {
            ResponseText = $"Не удалось загрузить модели: {ex.Message}";
        }
    }
}
