using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ControlTowerWin.Features.EmbeddedTerminal.Interfaces;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

/// <summary>
/// 앱-소유 임베드 터미널의 최상위 관리자 (FR-006/008/017, L0).
/// 탭(워크스페이스)들을 소유하고, 선택된 탭의 터미널들을 우측에 pane 분할로 표시한다.
/// 모든 탭·터미널 View는 살아있는 채(keep-alive) 유지된다.
/// </summary>
public class TerminalSessionsViewModel : ViewModelBase
{
    private TabViewModel? _selectedTab;
    private IRenamableNode? _selectedNode;
    private string _commandText = string.Empty;
    private int _counter;

    public ObservableCollection<TabViewModel> Tabs { get; } = new();
    public ICommand AddTabCommand { get; }
    public ICommand AddTerminalCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand InjectCommand { get; }

    /* 컨텍스트 메뉴 "명령 실행"이 커맨드 바에 포커스를 요청 → View가 처리 */
    public event Action? FocusCommandBarRequested;

    public TerminalSessionsViewModel()
    {
        AddTabCommand = new RelayCommand(_ => AddTab());
        AddTerminalCommand = new RelayCommand(_ => SelectedTab?.AddTerminal(), _ => SelectedTab != null);
        CloseCommand = new RelayCommand(_ => CloseSelected(), _ => SelectedTab != null);
        RenameCommand = new RelayCommand(_ => BeginRename(), _ => SelectedNode != null);
        InjectCommand = new RelayCommand(_ => InjectToSelected(),
            _ => HasInjectTargets() && !string.IsNullOrWhiteSpace(CommandText));
        /* "명령 실행" → 커맨드 바 포커스(실주입은 InjectCommand). 재시작=Runbook 09 스텁. */
        RunCommand = new RelayCommand(_ => FocusCommandBarRequested?.Invoke(), _ => SelectedTab?.SelectedTerminal != null);
        RestartCommand = new RelayCommand(_ => { /* TODO(09): RestartTerm/DisconnectConPTYTerm */ }, _ => SelectedTab != null);
        AddTab();
    }

    /* 커맨드 바 입력 텍스트 */
    public string CommandText
    {
        get => _commandText;
        set { _commandText = value; OnPropertyChanged(); }
    }

    /* 주입 대상이 있는지: 체크된 다중 대상 OR 선택 터미널 */
    private bool HasInjectTargets() =>
        Tabs.Any(t => t.Terminals.Any(x => x.IsInjectTarget)) || SelectedTab?.SelectedTerminal != null;

    /* 커맨드 주입(FR-014/FN-SES-04). 체크된 다중 대상 전부에 독립 주입, 없으면 선택 터미널 하나. */
    private void InjectToSelected()
    {
        var targets = Tabs.SelectMany(t => t.Terminals).Where(t => t.IsInjectTarget).ToList();
        if (targets.Count == 0)
        {
            var single = SelectedTab?.SelectedTerminal;
            if (single != null) targets.Add(single);
        }
        if (targets.Count == 0) return;

        foreach (var terminal in targets)
        {
            terminal.Inject(CommandText);
        }
        CommandText = string.Empty;
    }

    /* 좌측 트리에서 현재 선택된 노드(탭 또는 터미널) — 이름 변경 대상 */
    public IRenamableNode? SelectedNode
    {
        get => _selectedNode;
        set { _selectedNode = value; OnPropertyChanged(); }
    }

    /* 선택 노드를 인라인 편집 모드로 전환(FN-TRM-16) */
    private void BeginRename()
    {
        if (SelectedNode != null) SelectedNode.IsEditing = true;
    }

    /* 화면에 표시되는 탭(우측 pane 영역 노출 토글) */
    public TabViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            _selectedTab = value;
            OnPropertyChanged();
            foreach (var tab in Tabs)
            {
                tab.IsSelected = ReferenceEquals(tab, value);
            }
        }
    }

    private void AddTab()
    {
        var tab = new TabViewModel($"Tab {++_counter}");
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    /* 포커스 터미널이 있으면 그 터미널만, 없으면 탭 전체를 닫는다 */
    private void CloseSelected()
    {
        if (SelectedTab is null) return;

        var focused = SelectedTab.SelectedTerminal;
        if (focused != null && SelectedTab.Terminals.Count > 1)
        {
            SelectedTab.RemoveTerminal(focused);
            return;
        }

        var idx = Tabs.IndexOf(SelectedTab);
        Tabs.Remove(SelectedTab);
        SelectedTab = Tabs.Count == 0
            ? null
            : Tabs[System.Math.Min(idx, Tabs.Count - 1)];
    }
}