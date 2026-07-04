using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

/// <summary>
/// 하나의 임베드 터미널(라이브 ConPTY 세션)을 표한하는 ViewModel.
/// 탭에 귀속되어 좌측 트리에 자식 노드(PID 포함)로, 우측 pane으로 표시된다.
/// IsActive는 탭 내 포커스된 panem을 나타내어 테두리 강조.
/// </summary>
public class TerminalViewModel : ViewModelBase
{
    private bool _isActive;
    private int _pid;

    public string Title { get; }

    public TerminalViewModel(string title) => Title = title;

    /* 포커스된 pane(테두리 강조 대상 */
    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    /* ConPTY 자식(pwsh) 프로세스 ID. 컨트롤 기동후 할당됨 */
    public int Pid
    {
        get => _pid;
        set { _pid = value; OnPropertyChanged(); OnPropertyChanged(nameof(TreeLabel)); }
    }

    /* 좌측 트리에서 표시할 라벨. PID 포함 */
    public string TreeLabel => _pid > 0 ? $"{Title} (PID {_pid})" : $"{Title} (PID ...)";
}
