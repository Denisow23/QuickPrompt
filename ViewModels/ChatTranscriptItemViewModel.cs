using System;
using System.Windows;

namespace QuickPrompt.ViewModels;

public class ChatTranscriptItemViewModel : ViewModelBase
{
    private string _markdown;
    private bool _isPending;
    private bool _isError;
    private bool _hasAttachment;

    public ChatTranscriptItemViewModel(string role, string markdown, bool hasAttachment = false, bool isPending = false)
    {
        Role = role;
        _markdown = markdown;
        _hasAttachment = hasAttachment;
        _isPending = isPending;
        CreatedAt = DateTime.Now;
    }

    public string Role { get; }

    public DateTime CreatedAt { get; }

    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);

    public string SenderLabel => IsUser ? "Вы" : "QuickPrompt";

    public string TimeLabel => CreatedAt.ToString("HH:mm");

    public System.Windows.HorizontalAlignment BubbleAlignment =>
        IsUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left;

    public string Markdown
    {
        get => _markdown;
        set
        {
            _markdown = value;
            OnPropertyChanged();
        }
    }

    public bool HasAttachment
    {
        get => _hasAttachment;
        set
        {
            _hasAttachment = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasAttachmentPill));
        }
    }

    public bool HasAttachmentPill => HasAttachment;

    public bool IsPending
    {
        get => _isPending;
        set
        {
            _isPending = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStatePill));
            OnPropertyChanged(nameof(StatePillText));
        }
    }

    public bool IsError
    {
        get => _isError;
        set
        {
            _isError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStatePill));
            OnPropertyChanged(nameof(StatePillText));
        }
    }

    public bool HasStatePill => IsPending || IsError;

    public string StatePillText => IsPending ? "Думаю..." : IsError ? "Ошибка" : string.Empty;
}
