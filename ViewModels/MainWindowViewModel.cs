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
    private const string DefaultStatusText = "Контекст хранится локально, пока assistant открыт.";
    private const string NoAttachmentText = "Ничего не прикреплено";

    private readonly SettingsService _settingsService;
    private readonly OpenAiLikeClient _apiClient;
    private readonly ScreenshotService _screenshotService;
    private readonly List<ChatMessage> _sessionMessages = new();

    private AppSettings _settings;
    private string _promptText = string.Empty;
    private string _selectedModel;
    private string _statusText = DefaultStatusText;
    private string _attachmentStatus = NoAttachmentText;
    private string? _attachedScreenshotBase64;
    private bool _hasMessages;
    private bool _isSending;

    public event Action<AppSettings>? SettingsChanged;

    public MainWindowViewModel(AppSettings settings, SettingsService settingsService, OpenAiLikeClient apiClient, ScreenshotService screenshotService)
    {
        _settings = settings;
        _settingsService = settingsService;
        _apiClient = apiClient;
        _screenshotService = screenshotService;

        Overlay = new OverlayStateStore();
        Overlay.SetState(OverlayVisualState.Compact);

        _selectedModel = string.IsNullOrWhiteSpace(settings.DefaultModel) ? "gpt-4o-mini" : settings.DefaultModel;
        AddModelIfMissing(_selectedModel);

        SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
        NewChatCommand = new RelayCommand(() => ResetSession(showNotice: true), CanStartNewSession);
        CaptureScreenshotCommand = new RelayCommand(CaptureScreenshot);
        RemoveAttachmentCommand = new RelayCommand(ClearAttachment, () => HasAttachedScreenshot);
        PasteClipboardCommand = new RelayCommand(PasteClipboard);
        RefreshModelsCommand = new AsyncRelayCommand(RefreshModelsAsync);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        TogglePinCommand = new RelayCommand(() => Overlay.TogglePinned());
        CollapseCommand = new RelayCommand(() => Overlay.SetState(OverlayVisualState.Compact), () => !Overlay.IsPinned);

        Messages.CollectionChanged += (_, _) =>
        {
            HasMessages = Messages.Count > 0;
            NewChatCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(HasConversation));
            OnPropertyChanged(nameof(MessagesVisibility));
            OnPropertyChanged(nameof(TargetOverlayHeight));
            OnPropertyChanged(nameof(TargetOverlayWidth));

            if (Messages.Count > 0 && !_isSending)
            {
                Overlay.SetState(OverlayVisualState.Expanded);
            }
        };

        Overlay.PropertyChanged += (_, _) =>
        {
            CollapseCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(TargetOverlayHeight));
            OnPropertyChanged(nameof(TargetOverlayWidth));
            OnPropertyChanged(nameof(StatusAccentText));
            OnPropertyChanged(nameof(IsCompact));
            OnPropertyChanged(nameof(MessagesVisibility));
            OnPropertyChanged(nameof(AttachmentChipVisibility));
            OnPropertyChanged(nameof(ControlsVisibility));
            OnPropertyChanged(nameof(StatusVisibility));
        };
    }

    public OverlayStateStore Overlay { get; }

    public AppSettings CurrentSettings => _settings;

    public ObservableCollection<string> Models { get; } = new();

    public ObservableCollection<ChatTranscriptItemViewModel> Messages { get; } = new();

    public string PromptText
    {
        get => _promptText;
        set
        {
            _promptText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PromptPlaceholderVisibility));
            OnPropertyChanged(nameof(TargetOverlayHeight));
            SendCommand.RaiseCanExecuteChanged();
            NewChatCommand.RaiseCanExecuteChanged();

            if (string.IsNullOrWhiteSpace(_promptText) && !HasConversation && !_isSending)
            {
                Overlay.SetState(OverlayVisualState.Compact);
            }
            else if (!_isSending)
            {
                Overlay.SetState(OverlayVisualState.Focused);
            }
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

    public string StatusText
    {
        get => _statusText;
        private set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public string StatusAccentText => Overlay.IsGenerating ? "Thinking" : Overlay.IsPinned ? "Pinned" : "Ready";

    public string AttachmentStatus
    {
        get => _attachmentStatus;
        private set
        {
            _attachmentStatus = value;
            OnPropertyChanged();
        }
    }

    public bool HasAttachedScreenshot => !string.IsNullOrWhiteSpace(_attachedScreenshotBase64);

    public Visibility AttachmentVisibility => HasAttachedScreenshot ? Visibility.Visible : Visibility.Collapsed;

    public bool HasMessages
    {
        get => _hasMessages;
        private set
        {
            _hasMessages = value;
            OnPropertyChanged();
        }
    }

    public bool HasConversation => HasMessages || _isSending;

    public Visibility MessagesVisibility =>
        HasConversation && !Overlay.IsCompact ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AttachmentChipVisibility =>
        HasAttachedScreenshot ? Visibility.Visible : Visibility.Collapsed;

    public bool IsCompact => Overlay.IsCompact;

    public Visibility ControlsVisibility => Overlay.IsCompact ? Visibility.Collapsed : Visibility.Visible;

    public Visibility StatusVisibility => Overlay.IsCompact ? Visibility.Collapsed : Visibility.Visible;

    public Visibility PromptPlaceholderVisibility =>
        string.IsNullOrWhiteSpace(PromptText) ? Visibility.Visible : Visibility.Collapsed;

    public double TargetOverlayHeight
    {
        get
        {
            if (Overlay.IsCompact)
            {
                return 78;
            }

            var promptWeight = Math.Min(3, PromptText.Length / 100.0);
            var messageWeight = Math.Min(7, Messages.Count) * 46;
            var baseHeight = 158 + (promptWeight * 24);
            var composed = baseHeight + messageWeight;

            if (Overlay.IsGenerating)
            {
                composed += 30;
            }

            return Math.Clamp(composed, 180, 760);
        }
    }

    public double TargetOverlayWidth => Overlay.IsCompact ? 860 : 1200;

    public RelayCommand CaptureScreenshotCommand { get; }

    public RelayCommand PasteClipboardCommand { get; }

    public RelayCommand RemoveAttachmentCommand { get; }

    public RelayCommand NewChatCommand { get; }

    public RelayCommand OpenSettingsCommand { get; }

    public RelayCommand TogglePinCommand { get; }

    public RelayCommand CollapseCommand { get; }

    public AsyncRelayCommand SendCommand { get; }

    public AsyncRelayCommand RefreshModelsCommand { get; }

    public void ExpandFromUserActivity()
    {
        if (Overlay.IsHidden)
        {
            Overlay.SetState(OverlayVisualState.Compact);
        }

        if (_isSending)
        {
            Overlay.SetState(OverlayVisualState.Generating);
            return;
        }

        Overlay.SetState(OverlayVisualState.Focused);
    }

    public void CollapseIfIdle()
    {
        if (Overlay.IsPinned || _isSending)
        {
            return;
        }
        Overlay.SetState(OverlayVisualState.Compact);
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _apiClient.UpdateSettings(settings);

        if (!string.IsNullOrWhiteSpace(settings.DefaultModel))
        {
            AddModelIfMissing(settings.DefaultModel);
            if (string.IsNullOrWhiteSpace(SelectedModel))
            {
                SelectedModel = settings.DefaultModel;
            }
        }

        StatusText = "Настройки обновлены.";
        SettingsChanged?.Invoke(settings);
    }

    public void ResetSession(bool showNotice = false)
    {
        _sessionMessages.Clear();
        Messages.Clear();
        PromptText = string.Empty;
        ClearAttachment();
        Overlay.SetState(OverlayVisualState.Compact);
        StatusText = showNotice ? "Начата новая пустая сессия." : DefaultStatusText;
    }

    private async Task SendAsync()
    {
        if (!CanSend())
        {
            return;
        }

        _isSending = true;
        Overlay.SetState(OverlayVisualState.Generating);
        OnPropertyChanged(nameof(HasConversation));
        OnPropertyChanged(nameof(MessagesVisibility));
        OnPropertyChanged(nameof(TargetOverlayHeight));

        NewChatCommand.RaiseCanExecuteChanged();

        var prompt = PromptText.Trim();
        var hasAttachment = HasAttachedScreenshot;
        ChatMessage? userMessage = null;

        AddMessage(
            role: "user",
            markdown: BuildVisibleUserText(prompt, hasAttachment),
            hasAttachment: hasAttachment);

        var pendingAssistantBubble = AddMessage(
            role: "assistant",
            markdown: "…",
            isPending: true);

        try
        {
            var apiKey = _settingsService.Unprotect(_settings.EncryptedApiKey);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                pendingAssistantBubble.Markdown = "**Ошибка:** API-ключ не задан. Откройте настройки и сохраните ключ.";
                pendingAssistantBubble.IsPending = false;
                pendingAssistantBubble.IsError = true;
                StatusText = "Нужно сохранить API-ключ.";
                return;
            }

            userMessage = BuildUserMessage(prompt);
            _sessionMessages.Add(userMessage);

            var request = new ChatCompletionRequest
            {
                Model = string.IsNullOrWhiteSpace(SelectedModel) ? _settings.DefaultModel : SelectedModel,
                Messages = new List<ChatMessage>(_sessionMessages),
                Temperature = _settings.Temperature,
                MaxTokens = _settings.MaxTokens
            };

            StatusText = "Запрос отправлен. Жду ответ модели.";
            var assistantText = await _apiClient.SendChatAsync(request, apiKey);

            _sessionMessages.Add(new ChatMessage { Role = "assistant", Content = assistantText });
            pendingAssistantBubble.Markdown = assistantText;
            pendingAssistantBubble.IsPending = false;
            StatusText = "Ответ получен.";

            PromptText = string.Empty;
            ClearAttachment();
            Overlay.SetState(OverlayVisualState.Expanded);
        }
        catch (Exception ex)
        {
            if (userMessage is not null)
            {
                _sessionMessages.Remove(userMessage);
            }

            pendingAssistantBubble.Markdown = $"**Ошибка:** {ex.Message}";
            pendingAssistantBubble.IsPending = false;
            pendingAssistantBubble.IsError = true;
            StatusText = "Не удалось получить ответ модели.";
            Overlay.SetState(OverlayVisualState.Expanded);
        }
        finally
        {
            _isSending = false;
            NewChatCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(HasConversation));
            OnPropertyChanged(nameof(MessagesVisibility));
            OnPropertyChanged(nameof(TargetOverlayHeight));
        }
    }

    private ChatMessage BuildUserMessage(string prompt)
    {
        if (string.IsNullOrWhiteSpace(_attachedScreenshotBase64))
        {
            return new ChatMessage { Role = "user", Content = prompt };
        }

        var content = new List<MessageContentPart>();
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            content.Add(new MessageContentPart { Type = "text", Text = prompt });
        }

        content.Add(new MessageContentPart
        {
            Type = "image_url",
            ImageUrl = new ImageUrlWrapper { Url = $"data:image/png;base64,{_attachedScreenshotBase64}" }
        });

        return new ChatMessage { Role = "user", Content = content };
    }

    private void CaptureScreenshot()
    {
        try
        {
            _attachedScreenshotBase64 = _screenshotService.CapturePrimaryScreenAsBase64Png();
            AttachmentStatus = $"Скриншот прикреплен ({_attachedScreenshotBase64.Length / 1024} KB, base64).";
            OnPropertyChanged(nameof(HasAttachedScreenshot));
            OnPropertyChanged(nameof(AttachmentVisibility));
            OnPropertyChanged(nameof(AttachmentChipVisibility));
            SendCommand.RaiseCanExecuteChanged();
            NewChatCommand.RaiseCanExecuteChanged();
            RemoveAttachmentCommand.RaiseCanExecuteChanged();
            StatusText = "Скриншот готов к отправке.";
            Overlay.SetState(OverlayVisualState.Focused);
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось сделать скриншот: {ex.Message}";
        }
    }

    private void PasteClipboard()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsText())
            {
                return;
            }

            var text = System.Windows.Clipboard.GetText();
            var sb = new StringBuilder(PromptText);
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.Append(text);
            PromptText = sb.ToString();
            StatusText = "Текст из буфера обмена вставлен.";
            Overlay.SetState(OverlayVisualState.Focused);
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка буфера обмена: {ex.Message}";
        }
    }

    public async Task RefreshModelsAsync()
    {
        try
        {
            var apiKey = _settingsService.Unprotect(_settings.EncryptedApiKey);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                StatusText = "Сначала сохраните API-ключ в настройках.";
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
                AddModelIfMissing(_settings.DefaultModel);
            }

            if (string.IsNullOrWhiteSpace(SelectedModel) || !Models.Contains(SelectedModel))
            {
                SelectedModel = Models.Count > 0 ? Models[0] : _settings.DefaultModel;
            }

            StatusText = "Список моделей обновлен.";
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось загрузить модели: {ex.Message}";
        }
    }

    private bool CanSend()
    {
        return !string.IsNullOrWhiteSpace(PromptText) || HasAttachedScreenshot;
    }

    private bool CanStartNewSession()
    {
        return !_isSending && (HasMessages || !string.IsNullOrWhiteSpace(PromptText) || HasAttachedScreenshot);
    }

    private void ClearAttachment()
    {
        _attachedScreenshotBase64 = null;
        AttachmentStatus = NoAttachmentText;
        OnPropertyChanged(nameof(HasAttachedScreenshot));
        OnPropertyChanged(nameof(AttachmentVisibility));
        OnPropertyChanged(nameof(AttachmentChipVisibility));
        SendCommand.RaiseCanExecuteChanged();
        NewChatCommand.RaiseCanExecuteChanged();
        RemoveAttachmentCommand.RaiseCanExecuteChanged();
    }

    private ChatTranscriptItemViewModel AddMessage(string role, string markdown, bool hasAttachment = false, bool isPending = false)
    {
        var message = new ChatTranscriptItemViewModel(role, markdown, hasAttachment, isPending);
        Messages.Add(message);
        return message;
    }

    private string BuildVisibleUserText(string prompt, bool hasAttachment)
    {
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            return prompt;
        }

        return hasAttachment
            ? "_Отправлен скриншот без дополнительного текста._"
            : string.Empty;
    }

    private void OpenSettings()
    {
        if (System.Windows.Application.Current is App app)
        {
            var method = app.GetType().GetMethod("OpenSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(app, null);
        }
    }

    private void AddModelIfMissing(string model)
    {
        if (!string.IsNullOrWhiteSpace(model) && !Models.Contains(model))
        {
            Models.Add(model);
        }
    }
}
