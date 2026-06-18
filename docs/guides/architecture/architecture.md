# ControlTowerWin 아키텍처 — Feature × Layer 하이브리드 (NestJS 모듈 스타일)

## 개요

ControlTowerWin은 **WPF(.NET 10) 데스크톱 애플리케이션**으로, 여러 터미널/PowerShell 세션을
모니터링·제어하는 "관제탑(Control Tower)"이다.

디렉토리 구조는 **패턴1(Layer-based) × 패턴2(Feature-based)의 하이브리드**를 채택한다.
NestJS의 모듈 시스템과 동일한 철학으로:

- **최상위는 기능(Feature)으로 분할** — 한 기능의 모든 파일이 한 폴더에 모인다 (응집도)
- **각 기능 폴더 내부는 역할(Layer)로 재분할** — `Models / Views / ViewModels / Services` (예측가능성)

```
NestJS                         ControlTowerWin (WPF/MVVM)
──────────────────────────────────────────────────────────
*.module.ts        ↔  Features/<기능>/              (기능 경계)
*.controller.ts    ↔  ViewModels/                  (입력 수신·위임)
*.service.ts       ↔  Services/                    (비즈니스 로직)
dto / entity       ↔  Models/                      (데이터 형태)
SharedModule       ↔  Shared/                      (횡단 공유)
AppModule          ↔  Shell/                       (앱 호스트/진입)
```

> 본 문서는 **목표(target) 아키텍처**를 규정한다. 현재 코드는 `MainWindow.xaml.cs` 단일 파일에
> 집약되어 있으며, Runbook 04에서 이 구조로 분해한다. 각 컨벤션 문서에 "현재 → 목표" 매핑을 명시한다.

---

## 디렉토리 구조 (목표)

```
src/ControlTowerWin/
├── App.xaml(.cs)                    # 애플리케이션 진입점 (StartupUri)
├── AssemblyInfo.cs
├── ControlTowerWin.csproj
│
├── Shell/                           # 앱 셸 = NestJS AppModule
│   ├── Views/        MainWindow.xaml(.cs)      # 셸 윈도우(기능 View 호스트)
│   └── ViewModels/   MainWindowViewModel.cs    # 셸 VM(기능 VM 조합)
│
├── Features/                        # 기능 모듈 모음
│   ├── SessionMonitor/              # [기능] PowerShell 세션 추적
│   │   ├── Models/       TerminalInfo.cs
│   │   ├── Views/        SessionListView.xaml(.cs)
│   │   ├── ViewModels/   SessionListViewModel.cs
│   │   └── Services/     ProcessTracker.cs
│   │
│   ├── TerminalLauncher/            # [기능] 새 터미널 창 열기
│   │   ├── ViewModels/   NewTerminalViewModel.cs
│   │   └── Services/     TerminalLauncher.cs
│   │
│   └── SendCommand/                 # [기능] 세션에 커맨드 전송 (Runbook 05, 예정)
│       ├── Models/       CommandRequest.cs
│       ├── Views/        SendCommandView.xaml(.cs)
│       ├── ViewModels/   SendCommandViewModel.cs
│       └── Services/     IStdinInjector.cs / StdinInjector.cs
│
└── Shared/                          # 횡단 공유 자산 = SharedModule
    ├── Core/         ViewModelBase.cs, RelayCommand.cs
    ├── Controls/     (재사용 UserControl)
    ├── Converters/   (IValueConverter 모음)
    └── Styles/       (공유 ResourceDictionary)
```

### 3분할 고정 규칙

최상위는 항상 **`Shell / Features / Shared`** 셋만 둔다.

| 폴더 | NestJS 대응 | 책임 | 의존 방향 |
|------|------------|------|----------|
| `Shell/` | AppModule | 앱 셸 윈도우, 기능 View/VM 호스팅·조합, DI 조립 | → Features, → Shared |
| `Features/` | feature modules | 개별 기능 단위. 자기 폴더 내부에 레이어 보유 | → Shared (Feature 간 직접 참조 지양) |
| `Shared/` | common / SharedModule | 모든 곳에서 쓰는 베이스·유틸·리소스 | (의존 없음, 최하위) |

---

## 앱 시작 흐름 (현재 → 목표)

### 현재 (단일 파일)

```
┌────────────────────────────┐
│  App.xaml                  │
│  StartupUri="MainWindow"   │
└────────────┬───────────────┘
             │
┌────────────▼───────────────┐
│  MainWindow (xaml.cs)      │   ← 모든 것이 여기 집약
│  - TerminalInfo (model)    │
│  - DispatcherTimer 폴링    │
│  - Process.Exited 추적     │
│  - OnNewTerminalClick      │
└────────────────────────────┘
```

### 목표 (기능 분해)

```
┌────────────────────────────┐
│  App.xaml                  │
│  StartupUri=Shell/...       │
└────────────┬───────────────┘
             │
┌────────────▼───────────────────────────────┐
│  Shell / MainWindow + MainWindowViewModel  │
│  (기능 View들을 영역에 배치·조합)             │
└───┬───────────────────┬────────────────┬───┘
    │                   │                │
┌───▼─────────────┐ ┌───▼────────────┐ ┌─▼──────────────┐
│ SessionMonitor  │ │ TerminalLauncher│ │ SendCommand    │
│  ProcessTracker │ │  TerminalLauncher│ │  StdinInjector │
│  → SessionList  │ │  → New Terminal  │ │  → 커맨드 입력  │
└─────────────────┘ └─────────────────┘ └────────────────┘
        │                                       
   각 Service는 Shared/Core(ViewModelBase 등)에 의존
```

---

## 레이어별 개요 & 상세 컨벤션

| 레이어 | 문서 | 내용 |
|--------|------|------|
| Shell | [shell.md](../convention/shell.md) | 앱 셸 윈도우, 기능 조합, 진입점 |
| Feature 모듈 | [feature-module.md](../convention/feature-module.md) | 기능 폴더 경계·내부 레이어·점진 승격 규칙 |
| Models | [models.md](../convention/models.md) | 데이터 클래스, 표시용 파생 속성 |
| Views | [views.md](../convention/views.md) | XAML, x:Class·namespace 규약, DataTemplate |
| ViewModels | [viewmodels.md](../convention/viewmodels.md) | 입력 수신·바인딩, ViewModelBase, Command |
| Services | [services.md](../convention/services.md) | 프로세스 제어 등 비즈니스 로직, 인터페이스 분리 |
| Shared | [shared.md](../convention/shared.md) | Core/Controls/Converters/Styles 공유 자산 |

작업 절차는 [workflow/](../INDEX.md) 참조.

---

## 아키텍처 결정 요약표

| 항목 | 결정 | 근거 |
|------|------|------|
| 최상위 분할 | Shell / Features / Shared 고정 | NestJS app / feature / common 대응, 예측가능성 |
| 기능 내부 | Models/Views/ViewModels/Services 레이어 | 어느 기능을 열어도 동일 위치에 동일 역할 |
| 레이어 폴더 | **점진적 승격** (파일 1개 평면 → 2개↑ 폴더화) | 빈 폴더·1파일 폴더 양산 방지 |
| Feature 간 통신 | 직접 참조 지양, Shared/Core 경유 또는 Shell 조합 | 결합도 최소화, 모듈 격리 |
| VM↔View 매핑 | 명시적 DataTemplate / ViewModelLocator | 폴더 분산으로 자동 규칙(`*View`↔`*ViewModel`) 깨짐 대응 |
| 네임스페이스 | 폴더 경로와 일치 (`ControlTowerWin.Features.X.Views`) | x:Class 정합성, 탐색 직관성 |

---

## 주의사항

1. **점진적 레이어 승격**: 새 기능은 처음에 기능 폴더 루트에 평면 배치하고, 같은 역할 파일이
   2개 이상 모이면 그때 `Models/Services/...` 폴더로 승격한다. 처음부터 4폴더 강제 금지.
2. **네임스페이스-경로 일치**: 파일을 폴더로 옮기면 `x:Class`와 코드비하인드 `namespace`를
   폴더 경로에 맞춰 동시에 갱신해야 한다. 누락 시 XAML 컴파일 오류.
3. **Feature 간 직접 참조 금지**: 두 기능이 데이터를 공유해야 하면 `Shared/`로 올리거나 Shell이
   조합한다. `Features/A`가 `Features/B`를 직접 참조하면 모듈 격리가 깨진다.
4. **Shell은 얇게**: Shell은 기능 View/VM을 배치·조합만 한다. 비즈니스 로직을 Shell에 두지 않는다.
5. **AssemblyName 미지정 유지**: csproj는 파일명 기반으로 `ControlTowerWin` 어셈블리명을 자동 사용한다.
   루트 네임스페이스도 동일하게 유지한다.
