# Runbook 07 — 탭>터미널 다중 임베드 + pane 분할 (레거시 정리 포함)

> **방식**: Runbook 06과 동일 — **직접 구현·검증(PoC)한 절차를 런북으로 역산출**한다.
> 본 런북의 코드는 `.NET 10 / net10.0-windows`에서 빌드 0/0·실행 검증을 마친 것이다.
> 학습을 위해 **런북을 보며 직접 손으로 타이핑**하는 것을 전제로 상세히 기술한다.

---

## 목표

Runbook 06의 **단일** 임베드 터미널을, **탭 > 터미널 2계층 다중 임베드**로 확장한다.

- 여러 **탭**(워크스페이스)을 만들고, 각 탭이 **여러 터미널**을 소유한다.
- 좌측에 **트리**(탭 부모 → 터미널 자식, PID 표시), 탭을 고르면 그 탭의 터미널들이
  우측에 **pane 분할(타일)로 동시 표시**된다.
- **모든 탭·터미널의 ConPTY 세션은 탭을 전환해도 계속 살아있다**(keep-alive).
- 동시에, 기획과 어긋나는 **레거시 기능(전역 프로세스 감지·외부 WT 새창)을 폐기**한다.

> 기획 근거: FR-006(탭 컨테이너)·FR-008(탭 내 pane 분할)·FR-017(다중 세션 전환)·FR-039(앱-소유 세션 범위).

---

## 전제 (사전 조건)

- [ ] Runbook 06 완료 — `Features/EmbeddedTerminal/`의 `TerminalView`(EasyTerminalControl 호스팅) 동작,
      native(conpty.dll·OpenConsole.exe) 복사 포함 빌드 0/0.
- [ ] `Shared/Core`의 `ViewModelBase`·`RelayCommand` 존재(Runbook 04).
- [ ] 현재 `MainWindow`가 `SessionList`(SessionMonitor) + `Terminal`(단일) + `New Terminal Window` 버튼을 조합 중.

---

## 개념 (큰 틀)

### 1. 왜 레거시(SessionMonitor·TerminalLauncher)를 폐기하나

| 레거시 | 하던 일 | 폐기 이유 |
|--------|---------|-----------|
| `Features/SessionMonitor` (`ProcessTracker`) | 1초 폴링으로 **전역** pwsh/powershell 프로세스를 감지·목록화 | 앱이 spawn하지 않은 **외부** 프로세스를 관측 대상에 올림 → **FR-039 위반**(제어·관측은 앱-소유 세션 한정). ConPTY는 외부 프로세스 attach 불가라 어차피 제어도 못 함 |
| `Features/TerminalLauncher` (`wt -w -1 nt`) | 외부 **Windows Terminal 새 창**을 띄움 | 임베드 세션이 **유일한 터미널 실행 경로**여야 함(FR-039). 외부 새창은 v1 범위 밖 |

> 두 폴더를 통째로 삭제하고, 우리가 직접 spawn·소유하는 **앱-소유 세션 모델**만 남긴다.
> (삭제 코드는 git 이력에 남으므로 필요 시 참조 가능.)

### 2. 탭 > 터미널 2계층 모델

```
TerminalSessionsViewModel (최상위)
  └ Tabs : ObservableCollection<TabViewModel>
        └ TabViewModel (탭 = 워크스페이스)
              └ Terminals : ObservableCollection<TerminalViewModel>
                    └ TerminalViewModel (라이브 ConPTY 세션 1개 = pane 1개)
```

```
좌측 트리                        우측 (선택된 탭의 터미널 = pane 분할)
┌────────────────────┐   ┌──────────────┬──────────────┐
│ ▼ tab1             │   │ Terminal 1   │ Terminal 2   │
│    Terminal 1 (PID)│◀─┤ (pane)       │ (pane)       │
│    Terminal 2 (PID)│   ├──────────────┴──────────────┤
│ ▼ tab2             │   │ Terminal 3   (pane)         │
│    Terminal 1 (PID)│   └─────────────────────────────┘
└────────────────────┘      ← tab1의 터미널들만 타일로 동시 표시
```

### 3. keep-alive — **왜 `TabControl`을 안 쓰나** (이 런북의 핵심 함정)

WPF `TabControl`은 **`ContentPresenter`가 1개뿐**이라, 비활성 탭 콘텐츠를 **언로드/재생성**한다.
`EasyTerminalControl`은 native `HwndHost`라, View가 트리에서 제거되면 그 창이 파괴되고
**안에서 돌던 ConPTY(pwsh) 세션이 죽는다**. 탭을 왔다 갔다 할 때마다 세션이 재시작되면 못 쓴다.

그래서 **`ItemsControl` + Grid 겹침 + `Visibility` 토글**로 keep-alive를 만든다:

```
바깥 ItemsControl(ItemsSource = Tabs, 패널 = Grid)   ← 모든 탭을 같은 셀에 '겹쳐' 생성
   각 탭 컨테이너 Visibility = IsSelected ? Visible : Collapsed
      안쪽 ItemsControl(ItemsSource = Terminals, 패널 = UniformGrid)  ← 그 탭의 터미널을 타일
         각 터미널 = TerminalView(EasyTerminalControl)

핵심: ItemsControl은 항목 View를 '재활용/파괴'하지 않는다.
     → 비선택 탭·비표시 터미널의 View도 살아남아 ConPTY 유지.
     → Collapsed는 hwnd를 '숨김'일 뿐 파괴가 아니므로 세션 생존.
```

### 4. airspace / 트리 템플릿 충돌 주의

- **airspace**: native HwndHost 위에는 WPF를 겹칠 수 없다. 하지만 **타일은 서로 겹치지 않으므로 안전**하고,
  포커스 강조 **테두리(Border)** 는 터미널 native 영역 *바깥*이라 문제없다(오버레이 아님).
- **템플릿 충돌**: `TerminalViewModel`은 **좌측 트리 노드**로도, **우측 pane**으로도 쓰인다.
  암시적(`DataType`) `DataTemplate` 하나로 두면 트리에도 터미널이 통째로 렌더된다 → 잘못.
  → 트리용은 `HierarchicalDataTemplate.ItemTemplate`에 **가둬** 텍스트로, 우측용은 **키드(`x:Key`) 템플릿**으로 분리한다.
- **TreeView 선택**: `TreeView.SelectedItem`은 **읽기 전용**이라 바인딩 불가 → 코드비하인드 `SelectedItemChanged`로 VM에 반영한다.

---

## 아키텍처 통합 (Feature × Layer)

```
Features/EmbeddedTerminal/
├── ViewModels/
│   ├── TerminalViewModel.cs         # (수정) Title + IsActive(포커스) + Pid + TreeLabel
│   ├── TabViewModel.cs              # (신규) 탭 = 터미널 컬렉션 소유
│   └── TerminalSessionsViewModel.cs # (신규) 최상위 = 탭 컬렉션 소유·추가·닫기
└── Views/
    ├── TerminalView.xaml(.cs)       # (06 그대로) EasyTerminalControl 호스팅
    └── TerminalSessionsView.xaml(.cs) # (신규) 좌 트리 + 우 keep-alive 분할

Shell/
├── ViewModels/ MainWindowViewModel.cs  # (수정) TerminalSessionsViewModel만 보유
└── Views/      MainWindow.xaml          # (수정) 단일 ContentControl 조합
```

폐기: `Features/SessionMonitor/` · `Features/TerminalLauncher/` (통째 삭제).

---

## Step 1. 레거시 Feature 제거

`Features/SessionMonitor/`와 `Features/TerminalLauncher/` **폴더 전체를 삭제**한다.

```powershell
cd src/ControlTowerWin
git rm -r Features/SessionMonitor Features/TerminalLauncher
```

> Shell(`MainWindowViewModel`)이 이들을 `new` 하고 있으므로 **지금은 빌드가 깨진다**. Step 6에서 조합부를 교체하며 해소된다.

---

## Step 2. TerminalViewModel — 포커스·PID 추가

`Features/EmbeddedTerminal/ViewModels/TerminalViewModel.cs` (06의 빈 앵커를 아래로 교체)

```csharp
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

/// <summary>
/// 하나의 임베드 터미널(라이브 ConPTY 세션)을 표현하는 ViewModel.
/// 탭에 귀속되어 좌측 트리에 자식 노드(PID 포함)로, 우측엔 pane으로 표시된다.
/// IsActive는 탭 내 포커스된 pane을 나타내어 테두리로 강조된다.
/// 프로그램적 명령 주입(WriteToTerm)·출력 가로채기는 Runbook 08에서 이 VM에 추가한다.
/// </summary>
public class TerminalViewModel : ViewModelBase
{
    private bool _isActive;
    private int _pid;

    public string Title { get; }

    public TerminalViewModel(string title) => Title = title;

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

    /* 좌측 트리 표시 라벨 */
    public string TreeLabel => _pid > 0 ? $"{Title} (PID {_pid})" : $"{Title} (PID …)";
}
```

- `IsActive`: 좌측 트리에서 고른 터미널을 우측 pane 테두리로 강조하기 위한 플래그.
- `Pid`/`TreeLabel`: 트리에 `Terminal 1 (PID 12345)`처럼 표시. **PID 실값 연결은 후속**이라 지금은 `(PID …)`로 뜬다(구조만 준비).

---

## Step 3. TabViewModel — 탭(터미널 컬렉션 소유)

`Features/EmbeddedTerminal/ViewModels/TabViewModel.cs` (신규)

```csharp
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
            foreach (var t in Terminals)
                t.IsActive = ReferenceEquals(t, value);
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
        SelectedTerminal = Terminals.Count == 0
            ? null
            : Terminals[System.Math.Min(idx, Terminals.Count - 1)];
    }
}
```

- `IsSelected`: 우측에서 **이 탭이 보이는지** 토글(바깥 ItemsControl의 `Visibility` 바인딩 대상).
- `SelectedTerminal`: 탭 내부의 **포커스 pane**. 세팅 시 소속 터미널들의 `IsActive`를 갱신.
- 탭을 새로 만들면 터미널 1개를 자동 생성(빈 탭 방지).

---

## Step 4. TerminalSessionsViewModel — 최상위(탭 관리)

`Features/EmbeddedTerminal/ViewModels/TerminalSessionsViewModel.cs` (신규)

```csharp
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
                tab.IsSelected = ReferenceEquals(tab, value);
        }
    }

    private void AddTab()
    {
        var tab = new TabViewModel($"tab{++_counter}");
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
```

- `AddTabCommand` → 새 탭(+터미널 1). `AddTerminalCommand` → **선택된 탭에** 터미널 추가. `CloseCommand` → 포커스 터미널이 2개 이상 중 하나면 그 터미널만, 아니면 탭 전체를 닫음.
- `SelectedTab` 세팅 시 모든 탭의 `IsSelected` 갱신 → 우측에서 **선택 탭만** 보이게 된다.

---

## Step 5. TerminalSessionsView — 좌 트리 + 우 keep-alive 분할

> `TerminalView.xaml(.cs)`은 **06 그대로 재사용**한다(EasyTerminalControl 호스팅). 새로 만들지 않는다.

`Features/EmbeddedTerminal/Views/TerminalSessionsView.xaml` (신규)

```xml
<UserControl x:Class="ControlTowerWin.Features.EmbeddedTerminal.Views.TerminalSessionsView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:ControlTowerWin.Features.EmbeddedTerminal.ViewModels"
             xmlns:local="clr-namespace:ControlTowerWin.Features.EmbeddedTerminal.Views">
    <UserControl.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>

        <!-- 좌측 트리: 탭(부모) → 터미널(자식, PID 표시) -->
        <HierarchicalDataTemplate DataType="{x:Type vm:TabViewModel}"
                                  ItemsSource="{Binding Terminals}">
            <TextBlock Text="{Binding Title}" FontWeight="Bold"/>
            <HierarchicalDataTemplate.ItemTemplate>
                <DataTemplate DataType="{x:Type vm:TerminalViewModel}">
                    <TextBlock Text="{Binding TreeLabel}"/>
                </DataTemplate>
            </HierarchicalDataTemplate.ItemTemplate>
        </HierarchicalDataTemplate>

        <!-- 우측 pane: 라이브 터미널. 테두리는 native 영역 밖이라 airspace 무관. 활성 pane만 강조 -->
        <DataTemplate x:Key="TerminalPane" DataType="{x:Type vm:TerminalViewModel}">
            <Border Margin="2" BorderThickness="2" BorderBrush="Transparent">
                <Border.Style>
                    <Style TargetType="Border">
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding IsActive}" Value="True">
                                <Setter Property="BorderBrush" Value="#3B82F6"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </Border.Style>
                <local:TerminalView/>
            </Border>
        </DataTemplate>
    </UserControl.Resources>

    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="220"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- 좌측: 탭>터미널 트리 + 조작 -->
        <DockPanel Grid.Column="0">
            <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="0,0,0,4">
                <Button Content="새 탭" Command="{Binding AddTabCommand}" Margin="0,0,4,0" Padding="6,2"/>
                <Button Content="새 터미널" Command="{Binding AddTerminalCommand}" Margin="0,0,4,0" Padding="6,2"/>
                <Button Content="닫기" Command="{Binding CloseCommand}" Padding="6,2"/>
            </StackPanel>
            <TreeView x:Name="SessionTree" ItemsSource="{Binding Tabs}"
                      SelectedItemChanged="OnTreeSelectionChanged">
                <TreeView.ItemContainerStyle>
                    <Style TargetType="TreeViewItem">
                        <Setter Property="IsExpanded" Value="True"/>
                    </Style>
                </TreeView.ItemContainerStyle>
            </TreeView>
        </DockPanel>

        <!-- 우측: 탭들을 한 셀에 겹쳐 살려두고(keep-alive) 선택된 탭만 표시.
             선택된 탭 안에서 터미널들을 UniformGrid로 pane 분할 동시 표시(FR-006 탭 + FR-008 분할). -->
        <ItemsControl Grid.Column="1" Margin="8,0,0,0" ItemsSource="{Binding Tabs}">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                    <Grid/>
                </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.ItemContainerStyle>
                <Style TargetType="ContentPresenter">
                    <Setter Property="Visibility"
                            Value="{Binding IsSelected, Converter={StaticResource BoolToVis}}"/>
                </Style>
            </ItemsControl.ItemContainerStyle>
            <ItemsControl.ItemTemplate>
                <DataTemplate DataType="{x:Type vm:TabViewModel}">
                    <ItemsControl ItemsSource="{Binding Terminals}"
                                  ItemTemplate="{StaticResource TerminalPane}">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <UniformGrid/>
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                    </ItemsControl>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </Grid>
</UserControl>
```

### 왜 이렇게 짜는가 (한 줄씩)

- **`HierarchicalDataTemplate`**(탭) + 그 안의 `ItemTemplate`(터미널=텍스트): 트리 계층을 만들되,
  터미널 노드는 **텍스트로만** 그린다. 이 터미널 템플릿은 트리 안에 **갇혀** 있어 우측 pane과 충돌하지 않는다.
- **`x:Key="TerminalPane"`**: 우측 전용 템플릿(테두리 + `TerminalView`). 키가 있으므로 암시적으로 트리에 새지 않는다.
- **바깥 `ItemsControl`(패널=Grid)** + `ContentPresenter.Visibility = IsSelected`: 모든 탭을 겹쳐 살려두고 선택 탭만 표시.
- **안쪽 `ItemsControl`(패널=UniformGrid)**: 선택 탭의 터미널들을 자동 타일(1→1, 2→좌우, 3~4→2×2…).
- `IsExpanded=True`: 트리의 탭 노드를 펼쳐 터미널이 바로 보이게.

`Features/EmbeddedTerminal/Views/TerminalSessionsView.xaml.cs` (신규 — 트리 선택 반영)

```csharp
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

namespace ControlTowerWin.Features.EmbeddedTerminal.Views;

public partial class TerminalSessionsView : UserControl
{
    public TerminalSessionsView() => InitializeComponent();

    /* TreeView.SelectedItem은 읽기전용이라 바인딩 불가 → 코드비하인드에서 VM 선택 상태로 반영 */
    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not TerminalSessionsViewModel vm) return;

        switch (e.NewValue)
        {
            case TabViewModel tab:
                vm.SelectedTab = tab;
                break;
            case TerminalViewModel terminal:
                var parent = vm.Tabs.FirstOrDefault(t => t.Terminals.Contains(terminal));
                if (parent is not null)
                {
                    vm.SelectedTab = parent;
                    parent.SelectedTerminal = terminal;
                }
                break;
        }
    }
}
```

- 탭 노드 선택 → 그 탭을 표시. 터미널 노드 선택 → 소속 탭을 찾아 표시 + 그 터미널을 포커스(테두리).

---

## Step 6. Shell 조합

`Shell/ViewModels/MainWindowViewModel.cs` — 세션 관리자만 보유(레거시 3종 제거)

```csharp
using ControlTowerWin.Features.EmbeddedTerminal.ViewModels;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Shell.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public TerminalSessionsViewModel Sessions { get; }

    public MainWindowViewModel() => Sessions = new TerminalSessionsViewModel();
}
```

`Shell/Views/MainWindow.xaml` — 단일 ContentControl 조합

```xml
<Window x:Class="ControlTowerWin.Shell.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:etvm="clr-namespace:ControlTowerWin.Features.EmbeddedTerminal.ViewModels"
        xmlns:etv="clr-namespace:ControlTowerWin.Features.EmbeddedTerminal.Views"
        Title="Control Tower" Height="600" Width="900">
    <Window.Resources>
        <!-- VM-First 매핑: 세션 관리자 VM을 그릴 때 다중 세션 View 사용 -->
        <DataTemplate DataType="{x:Type etvm:TerminalSessionsViewModel}">
            <etv:TerminalSessionsView/>
        </DataTemplate>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="Control Tower"
                   HorizontalAlignment="Center" Margin="10" FontSize="24"/>

        <!-- 좌측 탭>터미널 트리 + 우측 임베드 터미널(다중, keep-alive) -->
        <ContentControl Grid.Row="1" Margin="10,0,10,10" Content="{Binding Sessions}"/>
    </Grid>
</Window>
```

> `SessionList`·`Terminal`(단일)·`NewTerminal` 바인딩과 그 `DataTemplate`을 모두 제거하고, `Sessions` 하나로 대체한다.
> `MainWindow.xaml.cs`는 06 그대로(`DataContext = new MainWindowViewModel()`).

---

## Step 7. 빌드 및 실행 검증

```powershell
cd src/ControlTowerWin
dotnet build ControlTowerWin.csproj
```

1. 빌드: **경고 0 / 오류 0** (레거시 참조가 남아 있으면 CS 오류 → Step 1·6 확인).
2. 실행: 탐색기에서 `bin/Debug/net10.0-windows/win-x64/ControlTowerWin.exe` 더블클릭.
   > 이전 인스턴스가 떠 있으면 exe가 잠겨 복사 실패(MSB3021) — 먼저 닫는다.
3. 검증:
   - 좌측 트리에 `tab1 > Terminal 1 (PID …)`, 우측에 pwsh 렌더.
   - **[새 터미널]** → tab1 아래 `Terminal 2`, 우측이 **2 pane 분할**.
   - 3~4개 → **2×2 타일** 동시 표시·각각 타이핑.
   - **[새 탭]** → `tab2` 전환, 우측이 tab2 것으로 교체.
   - tab1↔tab2 왕복 → **각 탭 터미널 모두 상태 유지**(keep-alive 핵심).
   - 트리에서 터미널 클릭 → 해당 pane **파란 테두리**.

---

## 체크리스트

- [ ] Step 1: `SessionMonitor`·`TerminalLauncher` 폴더 삭제(레거시)
- [ ] Step 2: `TerminalViewModel`에 `IsActive`·`Pid`·`TreeLabel`
- [ ] Step 3: `TabViewModel`(Terminals·IsSelected·SelectedTerminal·Add/RemoveTerminal)
- [ ] Step 4: `TerminalSessionsViewModel`(Tabs·SelectedTab·Add/Add터미널/Close 커맨드)
- [ ] Step 5: `TerminalSessionsView` 좌 TreeView + 우 바깥 ItemsControl(Grid,Visibility)/안쪽 ItemsControl(UniformGrid), 코드비하인드 `SelectedItemChanged`
- [ ] Step 6: `MainWindowViewModel`=`Sessions`만, `MainWindow`=단일 ContentControl
- [ ] Step 7: 빌드 0/0, 탭 전환 시 세션 유지(keep-alive), pane 타일·포커스 테두리

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| **탭 전환 시 터미널이 검정/재시작** | `TabControl` 사용 → 콘텐츠 재생성으로 ConPTY 파괴 | `ItemsControl`+Grid 겹침+`Visibility` keep-alive(개념 §3) |
| 트리 노드에 터미널이 통째로 렌더됨 | `TerminalViewModel` 암시적 DataTemplate이 트리에도 적용 | 트리용=`HierarchicalDataTemplate.ItemTemplate`, 우측용=`x:Key` 키드 템플릿으로 분리(§4) |
| 트리 선택이 VM에 반영 안 됨 | `TreeView.SelectedItem`은 읽기전용(바인딩 불가) | 코드비하인드 `SelectedItemChanged`로 반영(Step 5) |
| pane 위에 WPF가 안 겹쳐짐 | airspace(native HwndHost) | 정상. 타일은 안 겹치므로 무관, 테두리는 native 밖이라 OK |
| 빌드 CS 오류(SessionMonitor/TerminalLauncher 없음) | Shell이 삭제된 레거시를 참조 | Step 6의 `MainWindowViewModel`/`MainWindow` 교체 |
| 탭이 접혀 터미널이 안 보임 | TreeViewItem 기본 미확장 | `ItemContainerStyle`에 `IsExpanded=True` |

---

## 미결 / 후속

- **PID 실값 연결**: 지금은 `(PID …)` 자리표시자. `EasyTerminalControl`의 프로세스/ConPTY 핸들에서
  자식(pwsh) PID를 얻어 `TerminalViewModel.Pid`에 세팅하면 트리에 바로 반영된다(구조는 준비됨).
- **수동 pane 분할 핸들/크기 조정(FR-044)**: 현재는 `UniformGrid` 자동 타일. `GridSplitter` 기반 수동 분할은 후속.
- **세션 전환 단축키(FR-045, Ctrl+1…N)**: 좌측 트리/포커스와 연동하는 키 바인딩 후속.
- **탭/터미널 닫힘 시 좀비 정리**: View 제거 시 ConPTY 자식 종료 보장은 Runbook 09(수명 관리) 소관.

---

## 다음 단계

- **08**: 앱→터미널 **명령 주입** + **출력 가로채기/로깅**(`TermPTY.WriteToTerm`/`InterceptOutputToUITerminal`).
- **09**: 임베드 세션 **종료/수명** 관리(`RestartTerm`/`DisconnectConPTYTerm`) + 좀비 정리.

---

## 참고

- [Runbook 06 — 임베드 터미널 (PoC + 최소 임베드)](./06_wpf_embedded_terminal.md) — `TerminalView`/native 배포
- [Runbook 04 — 디렉토리 구조화](./04_wpf_directory_structuring.md) · [feature-module.md](../guides/convention/feature-module.md) · [viewmodels.md](../guides/convention/viewmodels.md) · [views.md](../guides/convention/views.md)
- 기획: [`04_requirements`](../plans/v1/04_requirements.md)(FR-006·008·044·045·039) · [`07_interfaces`](../plans/v1/07_interfaces.md)(SC-07·10) · [`12_roadmap`](../plans/v1/12_roadmap.md)(L0)
- [EasyWindowsTerminalControl (GitHub)](https://github.com/mitchcapper/EasyWindowsTerminalControl)
