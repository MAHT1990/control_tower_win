# Shell 컨벤션

## 개요

**Shell**은 앱의 진입·골격 레이어다. NestJS의 `AppModule`에 대응한다.
메인 윈도우를 띄우고, 각 Feature의 View/ViewModel을 화면 영역에 배치·조합한다.
**Shell 자체는 비즈니스 로직을 갖지 않는다** — 기능을 담는 액자일 뿐이다.

- 위치: `src/ControlTowerWin/Shell/`
- 책임: 앱 셸 윈도우, 기능 View 호스팅·레이아웃, (도입 시) DI 조립
- 진입: `App.xaml`의 `StartupUri` 또는 `App.xaml.cs`의 시작 로직이 Shell을 띄움

---

## 구조

```
Shell/
├── Views/       MainWindow.xaml(.cs)      # 셸 윈도우(기능 View 컨테이너)
└── ViewModels/  MainWindowViewModel.cs    # 기능 VM들을 조합/노출
```

```
App.xaml (StartupUri)
   │
   ▼
Shell/Views/MainWindow.xaml  ──DataContext──▶  Shell/ViewModels/MainWindowViewModel
   │  (영역에 기능 View 배치)
   ├─ SessionListView      (Features/SessionMonitor)
   ├─ New Terminal 버튼     (Features/TerminalLauncher)
   └─ SendCommandView       (Features/SendCommand, 예정)
```

---

## 사용 패턴

### 현재 → 목표

현재 `MainWindow`는 셸 역할과 기능 로직(세션 추적·터미널 실행)을 모두 떠안고 있다.
목표는 셸이 **조합만** 하고, 로직은 각 기능 VM/Service로 내보내는 것이다.

**Before (현재)** — 셸이 모든 것을 직접 수행
```csharp
// MainWindow.xaml.cs — 폴링·Exited·버튼 핸들러가 전부 여기
public ObservableCollection<TerminalInfo> Terminals { get; } = new();
private void OnNewTerminalClick(...) { Process.Start("wt", "-w -1 nt"); }
```

**After (목표)** — 셸 VM이 기능 VM을 보유·노출
```csharp
// Shell/ViewModels/MainWindowViewModel.cs
public SessionListViewModel SessionList { get; }   // Features/SessionMonitor
public NewTerminalViewModel NewTerminal { get; }   // Features/TerminalLauncher

public MainWindowViewModel(SessionListViewModel s, NewTerminalViewModel n)
{
    SessionList = s;
    NewTerminal = n;
}
```
```xml
<!-- Shell/Views/MainWindow.xaml — 기능 View를 영역에 배치 -->
<ContentControl Content="{Binding SessionList}" />
<Button Command="{Binding NewTerminal.OpenCommand}" Content="New Terminal Window" />
```

---

## 주의사항

1. **얇게 유지**: Shell에 비즈니스 로직(프로세스 제어, 데이터 가공)을 넣지 않는다. 조합·레이아웃만.
2. **StartupUri 경로**: `MainWindow.xaml`을 `Shell/Views/`로 옮기면 `App.xaml`의
   `StartupUri="MainWindow.xaml"`를 `StartupUri="Shell/Views/MainWindow.xaml"`로 갱신한다.
3. **x:Class 갱신**: 이동 후 `MainWindow`의 `x:Class`는 `ControlTowerWin.Shell.Views.MainWindow`가 된다.
   코드비하인드 `namespace`도 동일하게.
4. **DI는 점진 도입**: 지금은 생성자에서 직접 `new` 해도 무방하다. 기능이 늘면
   `Microsoft.Extensions.DependencyInjection`으로 Shell에서 조립하는 방향을 권장.
