# Runbook 03 — 새 Windows Terminal 창 열기 + 활성 PowerShell 세션 목록

## 목표

버튼 클릭 시 새 Windows Terminal 창을 열고,
현재 실행 중인 PowerShell 세션 목록을 실시간으로 표시한다.
PowerShell 창을 수동으로 닫으면 목록에서 즉시 제거된다.

---

## 개념

### Process.Start와 wt.exe

`wt`는 Windows Terminal의 앱 실행 별칭(App Execution Alias)이다.
Windows Terminal 내부에서 실행되는 실제 셸이 PowerShell이므로, 우리가 추적할 대상은 `powershell` / `pwsh` 프로세스다.

```
Process.Start("wt", "-w -1 nt")
  ↓
Windows Terminal 실행
  └─ 내부에서 PowerShell 실행
       ↓
  powershell.exe 또는 pwsh.exe 프로세스 생성
       ↓
  Process.GetProcessesByName("powershell" | "pwsh")  ← 추적 대상
```

> **PowerShell 버전별 프로세스명**
> - `powershell` — Windows PowerShell 5.x (Windows 내장)
> - `pwsh` — PowerShell 7+ (별도 설치)
>
> 둘 다 추적하여 버전에 무관하게 동작한다.

> **UseShellExecute = true 필요 이유**
> .NET Core 이상에서 기본값은 `false`다.
> Windows Terminal은 Store 앱 실행 별칭으로 등록되어 있어,
> `UseShellExecute = true` 없이는 "파일을 찾을 수 없음" 오류가 발생한다.

---

### DispatcherTimer — 새 프로세스 감지

WPF 전용 타이머. UI 스레드에서 직접 실행되므로 별도의 동기화 없이 UI를 업데이트할 수 있다.

```
DispatcherTimer (1초 간격)
  └─ Tick 이벤트 발생
       ├─ 새 PID 발견 → 목록 추가 + Exited 이벤트 구독
       └─ 사라진 PID → 목록 제거 (폴링 이중 안전망)
```

---

### Process.Exited — 프로세스 종료 즉시 감지

개별 `Process` 객체에 이벤트 핸들러를 등록하면, 해당 프로세스 종료 시 즉시 호출된다.

```csharp
process.EnableRaisingEvents = true;  // 이벤트 발화 활성화 (기본값 false)
process.Exited += (s, _) => { ... }; // 종료 시 즉시 호출
```

> **Exited는 별도 스레드에서 발화된다.**
> WPF UI 업데이트는 반드시 UI 스레드에서 해야 하므로
> `Dispatcher.Invoke`로 UI 스레드에 전달해야 한다.

---

### GC (Garbage Collection)와 Exited 이벤트

`Process` 객체를 지역 변수에만 두면 GC가 수거하여 `Exited` 이벤트가 발화되지 않는다.
특히 앱 실행 전부터 이미 떠 있던 프로세스는, 처음 감지 시점에만 `Process` 객체가 잠깐 생성됐다 사라지므로 이벤트 구독이 즉시 무효화된다.

```
[잘못된 패턴]                          [올바른 패턴]
foreach (var p in current)              _trackedProcesses[p.Id] = p;  ← 강한 참조 보관
{                                       p.EnableRaisingEvents = true;
    p.EnableRaisingEvents = true;       p.Exited += ...;
    p.Exited += ...;
    // 틱 종료 후 p는 지역변수
    // → GC 수거 → Exited 미발화
}
```

해결책: `Dictionary<int, Process>`에 저장하여 앱 수명 내내 강한 참조를 유지한다.

---

### ObservableCollection — 자동 UI 갱신

일반 `List<T>`는 변경 시 UI가 자동으로 갱신되지 않는다.
`ObservableCollection<T>`는 항목 추가/제거 시 바인딩된 UI를 자동으로 갱신한다.

```
ObservableCollection<TerminalInfo>
  항목 추가 / 제거
       ↓ 자동 알림 (INotifyCollectionChanged)
  ListBox UI 자동 갱신
```

---

### 실시간 추적 전략 요약

```
상황                         메커니즘                 반응 속도
──────────────────────────────────────────────────────────────
새 PowerShell 세션 감지      DispatcherTimer 폴링     최대 1초 내
PowerShell 수동 종료 감지    Process.Exited 이벤트    즉시
폴링 누락 분 최종 정리        폴링 이중 안전망          최대 1초 내
```

---

### UI 레이아웃 (3행 Grid)

```
┌────────────────────────────────┐
│         Hello World            │  ← Row 0  Height="Auto"
├────────────────────────────────┤
│  Active PowerShell Sessions    │  ← Row 1  Height="*"
│  ┌──────────────────────────┐  │
│  │ [powershell] PID 12345   │  │
│  │ [pwsh] PID 67890         │  │
│  └──────────────────────────┘  │
├────────────────────────────────┤
│     [New Terminal Window]      │  ← Row 2  Height="Auto"
└────────────────────────────────┘
```

---

## 사전 조건

- [ ] Runbook 01 완료 (Hello World 앱 정상 실행 확인)
- [ ] Windows Terminal 설치 확인 (`Win + R` → `wt` 실행되면 OK)

---

## Step 1. MainWindow.xaml 수정

`MainWindow.xaml`을 열고 아래로 교체 후 `Ctrl+S` 저장:

```xml
<Window x:Class="HelloWorldWpf.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Hello World" Height="300" Width="400">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0"
                   Text="Hello World"
                   HorizontalAlignment="Center"
                   Margin="10"
                   FontSize="32" />

        <StackPanel Grid.Row="1" Margin="10,0,10,0">
            <TextBlock Text="Active PowerShell Sessions"
                       FontWeight="Bold"
                       Margin="0,0,0,4" />
            <ListBox ItemsSource="{Binding Terminals}"
                     Height="120"
                     DisplayMemberPath="DisplayName" />
        </StackPanel>

        <Button Grid.Row="2"
                Content="New Terminal Window"
                Margin="10"
                Click="OnNewTerminalClick" />
    </Grid>
</Window>
```

| 코드 | 설명 |
|------|------|
| `Grid.RowDefinitions` 3행 | Auto / `*` / Auto — 가운데 행이 남은 공간 모두 차지 |
| `ListBox ItemsSource="{Binding Terminals}"` | `Terminals` 컬렉션과 데이터 바인딩 |
| `DisplayMemberPath="DisplayName"` | `TerminalInfo.DisplayName` 속성을 항목 텍스트로 표시 |
| `Click="OnNewTerminalClick"` | 버튼 클릭 시 코드 비하인드의 핸들러 연결 |

---

## Step 2. MainWindow.xaml.cs 수정

`MainWindow.xaml.cs`를 열고 아래로 교체 후 `Ctrl+S` 저장:

```csharp
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace HelloWorldWpf
{
    public class TerminalInfo
    {
        public int Pid { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string DisplayName => $"[{ProcessName}] PID {Pid}";
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<TerminalInfo> Terminals { get; } = new();

        private readonly DispatcherTimer _timer;
        private readonly Dictionary<int, Process> _trackedProcesses = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        private static readonly string[] PowerShellProcessNames = ["powershell", "pwsh"];

        private void OnTimerTick(object? sender, EventArgs e)
        {
            var current = PowerShellProcessNames
                .SelectMany(name => Process.GetProcessesByName(name))
                .ToArray();
            var currentPids = current.Select(p => p.Id).ToHashSet();

            foreach (var process in current)
            {
                if (!_trackedProcesses.ContainsKey(process.Id))
                {
                    _trackedProcesses[process.Id] = process;
                    Terminals.Add(new TerminalInfo { Pid = process.Id, ProcessName = process.ProcessName });

                    try
                    {
                        process.EnableRaisingEvents = true;
                        process.Exited += (s, _) =>
                        {
                            var pid = ((Process)s!).Id;
                            Dispatcher.Invoke(() => RemoveTerminal(pid));
                        };
                    }
                    catch { /* 권한 부족 프로세스는 폴링으로 감지 */ }
                }
            }

            var gone = _trackedProcesses.Keys.Except(currentPids).ToList();
            foreach (var pid in gone)
                RemoveTerminal(pid);
        }

        private void RemoveTerminal(int pid)
        {
            _trackedProcesses.Remove(pid);
            var item = Terminals.FirstOrDefault(t => t.Pid == pid);
            if (item != null) Terminals.Remove(item);
        }

        private void OnNewTerminalClick(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "wt",
                Arguments = "-w -1 nt",
                UseShellExecute = true
            });
        }
    }
}
```

| 코드 | 설명 |
|------|------|
| `TerminalInfo` | PID·프로세스명을 담는 데이터 클래스. `DisplayName`으로 ListBox 표시 텍스트 생성 |
| `ObservableCollection<TerminalInfo> Terminals` | 변경 시 ListBox UI를 자동으로 갱신하는 컬렉션 |
| `DataContext = this` | XAML `{Binding Terminals}`의 대상을 현재 Window로 지정 |
| `Dictionary<int, Process> _trackedProcesses` | PID → Process 매핑. 강한 참조로 GC 수거를 방지 |
| `DispatcherTimer` (1초) | UI 스레드에서 실행되는 타이머로 새 프로세스를 폴링 |
| `EnableRaisingEvents = true` | Exited 이벤트 발화 활성화 (기본값 `false`) |
| `try-catch` | 권한 부족 프로세스 접근 예외를 흡수하고 폴링으로 대체 감지 |
| `Dispatcher.Invoke` | Exited는 스레드 풀에서 발화 → UI 스레드로 전환 필수 |
| `RemoveTerminal(pid)` | dictionary + Terminals를 동시에 정리. Exited 핸들러와 폴링 양쪽에서 호출 |
| `gone` / `Except(currentPids)` | Exited 미발화 케이스를 폴링이 최종 정리하는 이중 안전망 |

---

## Step 3. 빌드 및 테스트

1. `Ctrl+Shift+B` — 빌드
2. `F5` — 실행
3. 이미 실행 중인 PowerShell 창이 있으면 목록에 자동 표시되는지 확인
4. **New Terminal Window** 버튼 클릭 → 최대 1초 내 목록에 추가되는지 확인
5. 터미널 창을 직접 닫기 → 즉시 목록에서 제거되는지 확인

---

## 체크리스트

- [ ] Step 1: MainWindow.xaml — 3행 Grid + ListBox 추가
- [ ] Step 2: MainWindow.xaml.cs — TerminalInfo / DispatcherTimer / Exited 구현
- [ ] Step 3: 빌드 성공
- [ ] Step 3: 기존 PowerShell 세션 자동 감지 확인
- [ ] Step 3: 버튼 클릭 → 목록 추가 확인
- [ ] Step 3: 창 수동 종료 → 즉시 목록 제거 확인

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| ListBox가 비어 있음 | `DataContext = this` 누락 | 생성자에 `DataContext = this` 추가 |
| 빌드 오류: `ToHashSet` / `FirstOrDefault` 없음 | `using System.Linq` 누락 | 파일 상단에 `using System.Linq;` 추가 |
| 앱 실행 전부터 있던 PowerShell은 닫아도 목록에서 안 사라짐 | GC 버그: `Process` 지역 변수 → GC 수거 → Exited 미발화 | `Dictionary<int, Process>`에 보관하여 강한 참조 유지 |
| 터미널 닫아도 목록에서 안 사라짐 | `EnableRaisingEvents = true` 누락 | 해당 코드 추가 확인 |
| UI 업데이트 중 크래시 | Exited 핸들러에서 직접 UI 접근 | `Dispatcher.Invoke` 로 감쌌는지 확인 |
| 버튼 클릭 시 "파일을 찾을 수 없음" 오류 | `UseShellExecute = true` 누락 | `ProcessStartInfo`에 `UseShellExecute = true` 추가 |
