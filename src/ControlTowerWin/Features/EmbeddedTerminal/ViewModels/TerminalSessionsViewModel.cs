using System.Collections.ObjectModel;
using System.Windows.Input;
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
    private int _counter;

    public ObservableCollection<TabViewModel> Tabs { get; } = new();
    public ICommand AddTabCommand { get; }
    public ICommand AddTerminalCommand { get; }
    public ICommand CloseCommand { get; }

    public TerminalSessionsViewModel()
    {
        AddTabCommand = new RelayCommand(_ => AddTab());
        AddTerminalCommand = new RelayCommand(_ => SelectedTab?.AddTerminal(), _ => SelectedTab != null);
        CloseCommand = new RelayCommand(_ => CloseSelected(), _ => SelectedTab != null);
        AddTab();
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