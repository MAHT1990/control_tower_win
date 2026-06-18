# Feature 모듈 컨벤션

## 개요

**Feature 모듈**은 ControlTowerWin 구조의 핵심 단위다. NestJS의 `*.module.ts`에 대응하며,
하나의 기능(예: 세션 모니터링, 새 터미널 열기)에 필요한 모든 코드를 **한 폴더**에 모은다.

- 위치: `src/ControlTowerWin/Features/<기능명>/`
- 책임: 한 기능의 데이터·화면·표현·로직을 자급자족(self-contained)으로 보유
- 내부는 다시 `Models / Views / ViewModels / Services` 레이어로 재분할

> "한 기능을 고치려면 한 폴더만 열면 된다" — 이것이 Feature 모듈의 목표다.

---

## 구조

```
Features/
├── SessionMonitor/              # 기능 1: PowerShell 세션 추적
│   ├── Models/      TerminalInfo.cs
│   ├── Views/       SessionListView.xaml(.cs)
│   ├── ViewModels/  SessionListViewModel.cs
│   └── Services/    ProcessTracker.cs
│
├── TerminalLauncher/            # 기능 2: 새 터미널 창 (파일 적어 평면 배치)
│   ├── NewTerminalViewModel.cs
│   └── TerminalLauncher.cs
│
└── SendCommand/                 # 기능 3: 커맨드 전송 (Runbook 05, 예정)
    ├── Models/      CommandRequest.cs
    ├── Views/       SendCommandView.xaml(.cs)
    ├── ViewModels/  SendCommandViewModel.cs
    └── Services/    StdinInjector.cs
```

### 내부 레이어와 네임스페이스

| 레이어 | 네임스페이스 | 상세 |
|--------|------------|------|
| Models | `ControlTowerWin.Features.SessionMonitor.Models` | [models.md](./models.md) |
| Views | `ControlTowerWin.Features.SessionMonitor.Views` | [views.md](./views.md) |
| ViewModels | `ControlTowerWin.Features.SessionMonitor.ViewModels` | [viewmodels.md](./viewmodels.md) |
| Services | `ControlTowerWin.Features.SessionMonitor.Services` | [services.md](./services.md) |

---

## 사용 패턴

### 점진적 레이어 승격 (핵심 규칙)

기능 폴더는 처음부터 4개 레이어 폴더를 만들지 않는다. 파일이 쌓이면 그때 승격한다.

```
[1단계] 파일 1개 → 기능 루트 평면 배치
TerminalLauncher/
└── TerminalLauncher.cs

[2단계] 같은 역할 2개↑ → 레이어 폴더 승격
TerminalLauncher/
├── ViewModels/  NewTerminalViewModel.cs
└── Services/    TerminalLauncher.cs   ← Service가 늘면 Services/로
```

**Good** — 작은 기능은 평면, 커지면 승격
```
SendCommand/StdinInjector.cs          # 처음엔 이렇게
SendCommand/Services/StdinInjector.cs # 주입기가 여러 개 되면 이렇게
SendCommand/Services/Win32Injector.cs
```

**Bad** — 파일 1개인데 4폴더 강제 (빈 폴더 양산)
```
SendCommand/
├── Models/       (비어 있음)
├── Views/        (비어 있음)
├── ViewModels/   (비어 있음)
└── Services/StdinInjector.cs
```

### 현재 → 목표 매핑

현재 `MainWindow.xaml.cs` 단일 파일의 책임이 다음 기능 모듈로 흩어진다.

| 현재 코드 (MainWindow.xaml.cs) | 목표 위치 |
|------------------------------|----------|
| `class TerminalInfo` | `Features/SessionMonitor/Models/TerminalInfo.cs` |
| `DispatcherTimer` 폴링 + `Process.Exited` 추적 | `Features/SessionMonitor/Services/ProcessTracker.cs` |
| `Terminals` 컬렉션 + 바인딩 | `Features/SessionMonitor/ViewModels/SessionListViewModel.cs` |
| `OnNewTerminalClick` (`wt -w -1 nt`) | `Features/TerminalLauncher/Services/TerminalLauncher.cs` |

---

## 주의사항

1. **Feature 간 직접 참조 금지**: `Features/A`가 `Features/B`의 클래스를 직접 참조하지 않는다.
   공유가 필요하면 `Shared/`로 올리거나 Shell이 조합한다. 직접 참조는 모듈 격리를 깬다.
2. **승격 시 네임스페이스 동시 갱신**: 파일을 레이어 폴더로 옮기면 `namespace`와(View라면) `x:Class`를
   폴더 경로에 맞춰 갱신해야 한다. 누락 시 빌드/XAML 오류.
3. **기능 단위 = 런북 단위**: 런북 1개가 기능 모듈 1개에 대응하도록 유지하면 문서-코드 추적이 쉽다.
4. **Service에 UI 의존 금지**: 기능의 `Services/`는 WPF 타입(Window, Dispatcher 등)에 의존하지 않는 것이
   이상적이다. UI 스레드 전환은 ViewModel 경계에서 처리한다. (현재 `ProcessTracker`는 과도기적으로
   `Dispatcher`를 쓸 수 있으나, 콜백/이벤트로 분리하는 방향을 권장)
