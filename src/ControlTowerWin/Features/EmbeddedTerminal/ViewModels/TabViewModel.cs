using System.Collections.ObjectModel;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

/// <summary>
/// 하나의 탭(워크스페이스). 여러 터미널(pane)을 소유하며, 선택된 탭의 터미널들이
/// 우측에 pane 분할로 동시 표시된다(FR-006 탭 + FR-008 탭 내 분할).
/// 비선택 탭의 터미널도 View는 살아남아 ConPTY 세션이 유지된다(keep-alive).
/// </summary>
public class TabViewModel : ViewModelBase
{
    private bool _isSelected;
    private TerminalViewModel? _selectedTerminal;
    private int _counter;

    public string Title { get; }

    public ObservableCollection<TerminalViewModel> Terminals { get; } = new();

    public TabViewModel(string title)
    {
        Title = title;
        AddTerminal();
    }

    /* 현재 화면에 표시되는 탭인지(우측 pane 영역 노출 토글) */
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    /* 탭 내 포커스된 터미널(pane 테두리 강조) */
    public TerminalViewModel? SelectedTerminal
    {
        get => _selectedTerminal;
        set
        {
            _selectedTerminal = value;
            OnPropertyChanged();
            foreach (var terminal in Terminals)
            {
                terminal.IsActive = ReferenceEquals(terminal, value);
            }
        }
    }

    public TerminalViewModel AddTerminal()
    {
        var terminal = new TerminalViewModel($"Terminal {++_counter}");
        Terminals.Add(terminal);
        SelectedTerminal = terminal;
        return terminal;
    }

    public void RemoveTerminal(TerminalViewModel terminal)
    {
        var idx = Terminals.IndexOf(terminal);
        if (idx < 0) return;
        Terminals.Remove(terminal);
        SelectedTerminal = Terminals.Count == 0 ? null : Terminals[System.Math.Min(idx, Terminals.Count - 1)];
    }
}
