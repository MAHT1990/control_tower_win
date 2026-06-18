# Runbook 04 — 디렉토리/모듈 구조화 (Feature × Layer 하이브리드)

## 목표

`MainWindow.xaml.cs` 한 파일에 모여 있는 코드를 **NestJS 모듈 스타일 하이브리드 구조**로 분해한다.
동작은 그대로 유지하면서(세션 목록 + 새 터미널 버튼), 코드를 `Shell / Features / Shared`로 나눈다.

> 구조 규약 전체는 [가이드 문서](../guides/INDEX.md) 참조. 본 런북은 그 가이드를 **손으로 실행**하는 절차다.

---

## 최종 디렉토리 (이 런북 완료 시)

```
src/ControlTowerWin/
├── App.xaml(.cs)                       # StartupUri + VM↔View 매핑
├── Shell/
│   └── Views/        MainWindow.xaml(.cs)
│       ViewModels/   MainWindowViewModel.cs
├── Features/
│   ├── SessionMonitor/
│   │   ├── Models/       TerminalInfo.cs
│   │   ├── Services/     ProcessTracker.cs
│   │   ├── ViewModels/   SessionListViewModel.cs
│   │   └── Views/        SessionListView.xaml(.cs)
│   └── TerminalLauncher/
│       ├── Services/     TerminalLauncher.cs
│       └── ViewModels/   NewTerminalViewModel.cs
└── Shared/
    └── Core/        ViewModelBase.cs, RelayCommand.cs
```

---

## 개념

### 왜 상향식(Shared → Features → Shell)인가

의존성은 **아래에서 위로** 흐른다. 가장 안쪽(의존 없는) 것부터 만들면, 다음 레이어를 만들 때
필요한 부품이 이미 존재한다.

```
Shared/Core   (아무것도 의존 안 함)        ← 1. 먼저
   ▲
Features      (Shared에 의존)              ← 2. 그다음
   ▲
Shell         (Features + Shared에 의존)   ← 3. 마지막
```

### SDK 스타일 프로젝트는 폴더를 자동 포함

`ControlTowerWin.csproj`는 SDK 스타일(`UseWPF=true`)이라 하위 폴더의 `*.cs`·`*.xaml`을
자동으로 컴파일에 포함한다. **새 폴더를 만들어도 csproj를 수정할 필요가 없다.**

### 핵심 책임 이동표

| 현재 (MainWindow.xaml.cs) | 이동 후 |
|---------------------------|---------|
| `class TerminalInfo` | `Features/SessionMonitor/Models/TerminalInfo.cs` |
| 폴링 + `Process.Exited` 추적 | `Features/SessionMonitor/Services/ProcessTracker.cs` |
| `Terminals` 컬렉션 + UI 갱신 | `Features/SessionMonitor/ViewModels/SessionListViewModel.cs` |
| 세션 목록 UI(ListBox) | `Features/SessionMonitor/Views/SessionListView.xaml` |
| `OnNewTerminalClick` (`wt -w -1 nt`) | `Features/TerminalLauncher/Services/TerminalLauncher.cs` |
| 버튼 클릭 → 명령 | `Features/TerminalLauncher/ViewModels/NewTerminalViewModel.cs` |
| `MainWindow`(셸) | `Shell/Views/MainWindow.xaml` + `Shell/ViewModels/MainWindowViewModel.cs` |

---

## 사전 조건

- [ ] Runbook 03 완료 (세션 목록 + 새 터미널 버튼 동작 확인)
- [ ] 프로젝트 리네임 완료 `ControlTowerWin` ([workflow/rename-project.md](../guides/workflow/rename-project.md))
- [ ] Visual Studio를 닫고 진행 권장 (파일 이동·삭제 잠금 방지)

---

## Step 1. Shared/Core — 기반 클래스

`src/ControlTowerWin/Shared/Core/` 폴더를 만들고 두 파일을 생성한다.

**`ViewModelBase.cs`** — 모든 ViewModel의 부모(값 변경을 UI에 통지)
```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ControlTowerWin.Shared.Core;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

**`RelayCommand.cs`** — 버튼 바인딩용 `ICommand`
```csharp
using System.Windows.Input;

namespace ControlTowerWin.Shared.Core;

public class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
```

> ✅ 체크포인트: 이 시점에 빌드하면 두 클래스만 추가된 상태로 통과해야 한다.

---

## Step 2. SessionMonitor 기능 — Model · Service

### 2-1. `Features/SessionMonitor/Models/TerminalInfo.cs`
현재 `MainWindow.xaml.cs`의 `TerminalInfo`를 그대로 옮긴다.
```csharp
namespace ControlTowerWin.Features.SessionMonitor.Models;

public class TerminalInfo
{
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName => $"[{ProcessName}] PID {Pid}";
}
```

### 2-2. `Features/SessionMonitor/Services/ProcessTracker.cs`
폴링 + `Process.Exited` 추적을 Service로 분리한다. **UI를 모른다** — 변화를 이벤트로만 알린다.
```csharp
using System.Diagnostics;
using ControlTowerWin.Features.SessionMonitor.Models;

namespace ControlTowerWin.Features.SessionMonitor.Services;

public class ProcessTracker
{
    private static readonly string[] Names = ["powershell", "pwsh"];
    private readonly Dictionary<int, Process> _tracked = new();   // 강한 참조: GC가 Exited 막는 것 방지
    private readonly System.Timers.Timer _timer = new(1000);
    private readonly object _lock = new();

    public event Action<TerminalInfo>? SessionAdded;
    public event Action<int>? SessionRemoved;   // pid

    public ProcessTracker() => _timer.Elapsed += (_, _) => Poll();

    public void Start() { Poll(); _timer.Start(); }
    public void Stop() => _timer.Stop();

    private void Poll()
    {
        var current = Names.SelectMany(Process.GetProcessesByName).ToArray();
        var currentPids = current.Select(p => p.Id).ToHashSet();

        lock (_lock)
        {
            foreach (var p in current)
            {
                if (_tracked.ContainsKey(p.Id)) continue;
                _tracked[p.Id] = p;
                SessionAdded?.Invoke(new TerminalInfo { Pid = p.Id, ProcessName = p.ProcessName });
                try
                {
                    p.EnableRaisingEvents = true;
                    p.Exited += (s, _) => Remove(((Process)s!).Id);
                }
                catch { /* 권한 부족 프로세스는 폴링으로 정리 */ }
            }

            foreach (var pid in _tracked.Keys.Except(currentPids).ToList())
                Remove(pid);
        }
    }

    private void Remove(int pid)
    {
        lock (_lock)
            if (!_tracked.Remove(pid)) return;
        SessionRemoved?.Invoke(pid);
    }
}
```

> 변경점: 원래 `DispatcherTimer`(UI 스레드) → `System.Timers.Timer`(백그라운드)로 바뀌어 Service가
> WPF에 의존하지 않는다. 대신 컬렉션 접근을 `lock`으로 보호하고, **UI 스레드 전환은 ViewModel이 담당**한다.

---

## Step 3. SessionMonitor 기능 — ViewModel · View

### 3-1. `Features/SessionMonitor/ViewModels/SessionListViewModel.cs`
Service 이벤트를 구독하고, UI 스레드로 전환해 `ObservableCollection`을 갱신한다.
```csharp
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using ControlTowerWin.Features.SessionMonitor.Models;
using ControlTowerWin.Features.SessionMonitor.Services;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.SessionMonitor.ViewModels;

public class SessionListViewModel : ViewModelBase
{
    public ObservableCollection<TerminalInfo> Sessions { get; } = new();
    private readonly Dispatcher _ui = Application.Current.Dispatcher;

    public SessionListViewModel(ProcessTracker tracker)
    {
        tracker.SessionAdded   += info => _ui.Invoke(() => Sessions.Add(info));
        tracker.SessionRemoved += pid  => _ui.Invoke(() =>
        {
            var item = Sessions.FirstOrDefault(s => s.Pid == pid);
            if (item != null) Sessions.Remove(item);
        });
        tracker.Start();
    }
}
```

### 3-2. `Features/SessionMonitor/Views/SessionListView.xaml` (UserControl)
원래 `MainWindow`의 ListBox 부분을 UserControl로 떼어낸다.
```xml
<UserControl x:Class="ControlTowerWin.Features.SessionMonitor.Views.SessionListView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <StackPanel>
        <TextBlock Text="Active PowerShell Sessions" FontWeight="Bold" Margin="0,0,0,4"/>
        <ListBox ItemsSource="{Binding Sessions}" Height="120" DisplayMemberPath="DisplayName"/>
    </StackPanel>
</UserControl>
```

### 3-3. `SessionListView.xaml.cs` (코드비하인드 — 최소)
```csharp
using System.Windows.Controls;

namespace ControlTowerWin.Features.SessionMonitor.Views;

public partial class SessionListView : UserControl
{
    public SessionListView() => InitializeComponent();
}
```

---

## Step 4. TerminalLauncher 기능 — Service · ViewModel

### 4-1. `Features/TerminalLauncher/Services/TerminalLauncher.cs`
```csharp
using System.Diagnostics;

namespace ControlTowerWin.Features.TerminalLauncher.Services;

public class TerminalLauncher
{
    public void OpenNewWindow() =>
        Process.Start(new ProcessStartInfo
        {
            FileName = "wt",
            Arguments = "-w -1 nt",
            UseShellExecute = true   // wt는 Store 앱 별칭 → 반드시 true
        });
}
```

### 4-2. `Features/TerminalLauncher/ViewModels/NewTerminalViewModel.cs`
```csharp
using System.Windows.Input;
using ControlTowerWin.Features.TerminalLauncher.Services;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.TerminalLauncher.ViewModels;

public class NewTerminalViewModel : ViewModelBase
{
    public ICommand OpenCommand { get; }

    // ⚠️ 클래스명 TerminalLauncher가 폴더 네임스페이스(...Features.TerminalLauncher)와 같다.
    //    이 ViewModel은 그 네임스페이스 하위에 있어, 'TerminalLauncher'만 쓰면 컴파일러가
    //    타입이 아닌 '네임스페이스'로 해석한다(CS0118). 'Services.'로 한정해 타입을 지목한다.
    public NewTerminalViewModel(Services.TerminalLauncher launcher) =>
        OpenCommand = new RelayCommand(_ => launcher.OpenNewWindow());
}
```

> **네이밍 주의**: 기능 폴더명과 그 안의 대표 클래스명이 같으면(여기선 둘 다 `TerminalLauncher`)
> 동일 네임스페이스 트리 안에서 충돌한다. NestJS가 `UsersService`처럼 역할 접미사를 붙이는 이유다.
> 한정(`Services.TerminalLauncher`)으로 피하거나, 클래스명에 역할 접미사를 줄 수 있다.

---

## Step 5. Shell — 기능 조합

### 5-1. `Shell/ViewModels/MainWindowViewModel.cs`
기능 VM들을 보유·노출한다. (지금은 수동 `new`, DI는 나중에)
```csharp
using ControlTowerWin.Features.SessionMonitor.Services;
using ControlTowerWin.Features.SessionMonitor.ViewModels;
using ControlTowerWin.Features.TerminalLauncher.Services;
using ControlTowerWin.Features.TerminalLauncher.ViewModels;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Shell.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public SessionListViewModel SessionList { get; }
    public NewTerminalViewModel NewTerminal { get; }

    public MainWindowViewModel()
    {
        SessionList = new SessionListViewModel(new ProcessTracker());
        NewTerminal = new NewTerminalViewModel(new TerminalLauncher());
    }
}
```

### 5-2. `Shell/Views/MainWindow.xaml`
기존 `MainWindow.xaml`을 `Shell/Views/`로 옮기고 아래로 교체한다.
`x:Class`가 `ControlTowerWin.Shell.Views.MainWindow`로 바뀌는 점에 주의.
```xml
<Window x:Class="ControlTowerWin.Shell.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:smvm="clr-namespace:ControlTowerWin.Features.SessionMonitor.ViewModels"
        xmlns:smv="clr-namespace:ControlTowerWin.Features.SessionMonitor.Views"
        Title="Control Tower" Height="300" Width="400">
    <Window.Resources>
        <!-- VM-First 매핑: SessionListViewModel을 그릴 때 SessionListView 사용 -->
        <DataTemplate DataType="{x:Type smvm:SessionListViewModel}">
            <smv:SessionListView/>
        </DataTemplate>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="Control Tower"
                   HorizontalAlignment="Center" Margin="10" FontSize="32"/>

        <!-- 기능 View를 영역에 주입 (ContentControl + DataTemplate) -->
        <ContentControl Grid.Row="1" Margin="10,0,10,0" Content="{Binding SessionList}"/>

        <!-- Click 핸들러 대신 Command 바인딩 -->
        <Button Grid.Row="2" Content="New Terminal Window" Margin="10"
                Command="{Binding NewTerminal.OpenCommand}"/>
    </Grid>
</Window>
```

### 5-3. `Shell/Views/MainWindow.xaml.cs`
폴링·이벤트·핸들러가 전부 빠지고 **DataContext 연결만** 남는다.
```csharp
using System.Windows;
using ControlTowerWin.Shell.ViewModels;

namespace ControlTowerWin.Shell.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
```

---

## Step 6. App.xaml — 진입점 경로 갱신

`App.xaml`의 `StartupUri`를 새 위치로 바꾼다.
```xml
<Application x:Class="ControlTowerWin.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:ControlTowerWin"
             StartupUri="Shell/Views/MainWindow.xaml">
    <Application.Resources>
    </Application.Resources>
</Application>
```

> 옛 `MainWindow.xaml` / `MainWindow.xaml.cs`(루트)는 Shell로 옮겼으니 **루트에서 삭제**한다.

---

## Step 7. 빌드 및 테스트

```powershell
cd src/ControlTowerWin
dotnet build ControlTowerWin.csproj
```

1. 빌드: 경고 0 / 오류 0
2. 실행(`dotnet run` 또는 F5)
3. 기존 PowerShell 창이 목록에 자동 표시되는지
4. **New Terminal Window** 클릭 → 최대 1초 내 목록 추가
5. 터미널 창 수동 종료 → 즉시 목록에서 제거

---

## 체크리스트

- [ ] Step 1: `Shared/Core` — ViewModelBase, RelayCommand
- [ ] Step 2: `SessionMonitor` — TerminalInfo(Model), ProcessTracker(Service)
- [ ] Step 3: `SessionMonitor` — SessionListViewModel(VM), SessionListView(View)
- [ ] Step 4: `TerminalLauncher` — TerminalLauncher(Service), NewTerminalViewModel(VM)
- [ ] Step 5: `Shell` — MainWindowViewModel, MainWindow(View) 이동
- [ ] Step 6: App.xaml StartupUri 갱신 + 루트 MainWindow 삭제
- [ ] Step 7: 빌드 통과 + 3가지 동작(자동감지/추가/제거) 확인

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| 빌드 오류: `x:Class`가 namespace와 불일치 | 파일 이동 후 `x:Class` 또는 `namespace` 한쪽만 변경 | 둘을 폴더 경로에 맞춰 동시 갱신 |
| 실행 시 빈 창 / 목록 안 뜸 | `DataContext = new MainWindowViewModel()` 누락 | MainWindow 코드비하인드에 추가 |
| ListBox 비어 있음(VM은 있음) | `DataTemplate` 매핑 누락 → ContentControl이 VM을 텍스트로 그림 | `Window.Resources`에 `DataTemplate DataType=SessionListViewModel` 추가 |
| 버튼 눌러도 반응 없음 | `Click=` 제거했는데 `Command` 바인딩 경로 오타 | `{Binding NewTerminal.OpenCommand}` 확인 |
| 세션 추가/제거 시 크래시 | 백그라운드 스레드에서 `ObservableCollection` 직접 수정 | VM에서 `Dispatcher.Invoke`로 감쌌는지 확인 |
| 앱 시작 안 됨 / 창 안 뜸 | `App.xaml` StartupUri가 옛 경로 | `StartupUri="Shell/Views/MainWindow.xaml"` 확인 |
| "파일을 찾을 수 없음"(새 터미널) | `UseShellExecute=true` 누락 | TerminalLauncher의 ProcessStartInfo 확인 |
| CS0118: `TerminalLauncher`은 네임스페이스이지만 형식처럼 사용됨 | 클래스명 == 폴더 네임스페이스명 충돌 | `Services.TerminalLauncher`로 한정 (Step 4-2 노트) |
| CS7025/CS0053: 일관성 없는 액세스 가능성 | Model을 `internal`로 선언 → public 멤버가 노출 | `TerminalInfo`를 `public class`로 |
| 빌드는 되는데 옛 화면이 뜸 / 중복 클래스 | 루트 옛 `MainWindow.xaml(.cs)` 미삭제 | Step 6대로 루트 파일 삭제 (`git rm`) |

---

## 다음 단계

- 기능 추가는 [add-new-feature 워크플로우](../guides/workflow/add-new-feature.md)를 따른다.
- Runbook 05 (세션에 커맨드 전송)는 `Features/SendCommand/` 모듈로 신설한다.
