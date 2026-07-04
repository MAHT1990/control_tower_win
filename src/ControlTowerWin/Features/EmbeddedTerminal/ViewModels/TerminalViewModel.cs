using ControlTowerWin.Features.EmbeddedTerminal.Interfaces;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

/// <summary>
/// 하나의 임베드 터미널(라이브 ConPTY 세션)을 표현하는 ViewModel.
/// 탭에 귀속되어 좌측 트리에 자식 노드(PID 포함)로, 우측엔 pane으로 표시된다.
/// IsActive는 탭 내 포커스된 pane을 나타내어 테두리로 강조된다.
/// Title은 표시 이름(FR-046, 런타임 편집 가능·as와 분리). 명령 주입은 Runbook 08에서 추가한다.
/// </summary>
public class TerminalViewModel : ViewModelBase, IRenamableNode
{
    private bool _isActive;
    private bool _isEditing;
    private int _pid;
    private string _title;

    public TerminalViewModel(string title) => _title = title;

    /* 표시 이름(디스플레이 라벨). 빈 문자열은 거부(이전 이름 유지). */
    public string Title
    {
        get => _title;
        set
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return;
            _title = trimmed;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TreeLabel));
        }
    }

    /* 좌측 트리 인라인 편집 모드 토글 */
    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(); }
    }

    /* 포커스된 pane(테두리 강조 대상) */
    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    /* ConPTY 자식(pwsh) 프로세스 ID. 컨트롤 기동 후 채워진다(0=미확정) */
    public int Pid
    {
        get => _pid;
        set { _pid = value; OnPropertyChanged(); OnPropertyChanged(nameof(TreeLabel)); }
    }

    /* 좌측 트리 표시 라벨 (표시명 + PID) */
    public string TreeLabel => _pid > 0 ? $"{Title} (PID {_pid})" : $"{Title} (PID ...)";
}
