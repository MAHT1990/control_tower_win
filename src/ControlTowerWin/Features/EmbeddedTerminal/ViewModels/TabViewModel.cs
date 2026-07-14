using System.Collections.ObjectModel;
using ControlTowerWin.Features.EmbeddedTerminal.Interfaces;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

/// <summary>
/// 하나의 탭(워크스페이스). 여러 터미널(pane)을 소유하며, 선택된 탭의 터미널들이
/// 우측에 pane 분할로 동시 표시된다(FR-006 탭 + FR-008 탭 내 분할).
/// 비선택 탭의 터미널도 View는 살아남아 ConPTY 세션이 유지된다(keep-alive).
/// </summary>
public class TabViewModel : ViewModelBase, IRenamableNode
{
    private bool _isSelected;
    private bool _isEditing;
    private string _title;
    private TerminalViewModel? _selectedTerminal;
    private int _counter;

    public ObservableCollection<TerminalViewModel> Terminals { get; } = new();

    public TabViewModel(string title)
    {
        _title = title;
        AddTerminal();
    }

    /* 표시 이름(FR-046, 런타임 편집 가능). 빈 문자열은 거부(이전 이름 유지). */
    public string Title
    {
        get => _title;
        set
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return;
            _title = trimmed;
            OnPropertyChanged();
        }
    }

    /* 좌측 트리 인라인 편집 모드 토글 */
    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(); }
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
