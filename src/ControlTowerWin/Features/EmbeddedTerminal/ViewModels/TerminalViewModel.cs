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
    private bool _isInjectTarget;
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

    /* 다중 주입 대상 체크(FR-014 다중 세션 독립 주입) */
    public bool IsInjectTarget
    {
        get => _isInjectTarget;
        set { _isInjectTarget = value; OnPropertyChanged(); }
    }

    /* ConPTY 자식(pwsh) 프로세스 ID. 컨트롤 기동 후 채워진다(0=미확정) */
    public int Pid
    {
        get => _pid;
        set { _pid = value; OnPropertyChanged(); OnPropertyChanged(nameof(TreeLabel)); }
    }

    /* 좌측 트리 표시 라벨 (표시명 + PID) */
    public string TreeLabel => _pid > 0 ? $"{Title} (PID {_pid})" : $"{Title} (PID ...)";

    /* 엔진 경계 세션(주입/캡처). View 로드 시 EasyTerminalControl에서 주입됨(NFR-018). */
    private ITerminalSession? _session;

    /* 세션 attach 전에 요청된 글꼴(신규 터미널·앱 시작 시). attach 시점에 적용된다. */
    private (string Family, int Size)? _pendingFont;

    public void AttachSession(ITerminalSession session)
    {
        _session = session;
        if (_pendingFont is { } font)
        {
            session.ApplyFont(font.Family, font.Size);
        }
    }

    /* 글꼴 적용(FR-048). 세션 미준비면 보관했다가 attach 시 적용. */
    public void ApplyFont(string fontFamily, int fontSize)
    {
        _pendingFont = (fontFamily, fontSize);
        _session?.ApplyFont(fontFamily, fontSize);
    }

    /* 커맨드 주입(FR-014/FN-SES-04). 개행을 붙여 실행한다. */
    public void Inject(string command)
    {
        if (_session is null || string.IsNullOrWhiteSpace(command)) return;
        _session.Send(command + "\r");
    }

    /* 현재 출력 캡처(FR-002 → FR-047 캡처 버퍼 소스, Task 4) */
    public string CaptureOutput() => _session?.GetOutputText() ?? string.Empty;

    /* 세션 재시작(FN-SES-10) */
    public void Restart() => _session?.Restart();

    /* 세션 종료·정리(FN-TRM-02, 좀비 방지). View 언로드·앱 종료 시 호출. */
    public void Cleanup() => _session?.Close();
}
