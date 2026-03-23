using QuickPrompt.Helpers;

namespace QuickPrompt.ViewModels;

public enum OverlayVisualState
{
    Compact,
    Focused,
    Generating,
    Expanded,
    Hidden
}

public class OverlayStateStore : ViewModelBase
{
    private OverlayVisualState _state = OverlayVisualState.Compact;
    private bool _isPinned;

    public OverlayVisualState State
    {
        get => _state;
        private set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCompact));
            OnPropertyChanged(nameof(IsExpanded));
            OnPropertyChanged(nameof(IsGenerating));
            OnPropertyChanged(nameof(IsHidden));
        }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned == value)
            {
                return;
            }

            _isPinned = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PinGlyph));
        }
    }

    public bool IsCompact => State == OverlayVisualState.Compact;

    public bool IsExpanded => State is OverlayVisualState.Expanded or OverlayVisualState.Focused or OverlayVisualState.Generating;

    public bool IsGenerating => State == OverlayVisualState.Generating;

    public bool IsHidden => State == OverlayVisualState.Hidden;

    public string PinGlyph => IsPinned ? "📌" : "⟂";

    public void SetState(OverlayVisualState state)
    {
        if (IsPinned && state == OverlayVisualState.Compact)
        {
            State = OverlayVisualState.Expanded;
            return;
        }

        State = state;
    }

    public void TogglePinned()
    {
        IsPinned = !IsPinned;
        if (IsPinned && State == OverlayVisualState.Compact)
        {
            State = OverlayVisualState.Expanded;
        }
    }
}
