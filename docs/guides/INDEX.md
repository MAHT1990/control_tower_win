# ControlTowerWin 가이드 문서 인덱스

## 개요

ControlTowerWin(여러 터미널/PowerShell 세션을 관제하는 WPF .NET 10 데스크톱 앱)의
**디렉토리·모듈 구조 가이드**다. 구조는 **Feature × Layer 하이브리드(NestJS 모듈 스타일)** —
최상위를 `Shell / Features / Shared`로 나누고, 각 기능 폴더 내부를 `Models / Views / ViewModels / Services`
레이어로 재분할한다. 본 가이드는 Runbook 04(디렉토리 구조화)의 선행 기준 문서다.

> 현재 코드는 `MainWindow.xaml.cs` 단일 파일에 집약되어 있으며, 본 가이드의 목표 구조로 분해 예정.

---

## 문서 트리

```
docs/guides/
├── architecture/
│   └── architecture.md          # 전체 구조 개요·다이어그램·결정표
├── convention/
│   ├── shell.md                 # 앱 셸(진입·기능 조합)
│   ├── feature-module.md        # 기능 폴더 경계·내부 레이어·점진 승격 (핵심)
│   ├── models.md                # 데이터 클래스·파생 속성
│   ├── interfaces.md            # 계약(interface) 전용 레이어·항상 분리
│   ├── views.md                 # XAML·x:Class/namespace·DataTemplate
│   ├── viewmodels.md            # ViewModelBase·Command·바인딩
│   ├── services.md              # 프로세스 제어 로직(구현체)
│   ├── shared.md                # Core/Controls/Converters/Styles
│   └── comments.md              # 주석 스타일(/* */, ///, 인라인 금지)
├── workflow/
│   ├── rename-project.md        # 프로젝트 리네임 절차
│   └── add-new-feature.md       # 신규 기능 모듈 추가 절차
└── INDEX.md                     # (이 문서)
```

---

## 아키텍처

| 문서 | 내용 |
|------|------|
| [architecture.md](./architecture/architecture.md) | 하이브리드 구조 전체 개요, 시작 흐름 다이어그램, 결정 요약표 |

## 컨벤션

### 구조 단위
| 문서 | 내용 |
|------|------|
| [shell.md](./convention/shell.md) | 앱 셸 윈도우, 기능 View 호스팅, 진입점 |
| [feature-module.md](./convention/feature-module.md) | 기능 폴더 경계, 점진적 레이어 승격, 현재→목표 매핑 |

### 기능 내부 레이어 (MVVM)
| 문서 | 내용 |
|------|------|
| [models.md](./convention/models.md) | 순수 데이터, 표시용 파생 속성 |
| [interfaces.md](./convention/interfaces.md) | 계약(interface) 전용 레이어, 항상 분리(개수 무관), 계약↔구현 분리 |
| [views.md](./convention/views.md) | XAML, x:Class/namespace 규약, UserControl 분리 |
| [viewmodels.md](./convention/viewmodels.md) | 바인딩 속성·ICommand, ViewModelBase, UI 스레드 전환 |
| [services.md](./convention/services.md) | 프로세스 추적·실행, GC 방지 강한 참조, 구현체(계약은 Interfaces) |

### 횡단 공유
| 문서 | 내용 |
|------|------|
| [shared.md](./convention/shared.md) | ViewModelBase, RelayCommand, Converter, 공유 Style |

### 코딩 규약
| 문서 | 내용 |
|------|------|
| [comments.md](./convention/comments.md) | C# 주석 `/* */`, XML 문서 `///`, 인라인(같은 줄) 주석 금지 |

## Workflow

| 문서 | 내용 |
|------|------|
| [rename-project.md](./workflow/rename-project.md) | 프로젝트 리네임(HelloWorldWpf→ControlTowerWin) 5단계 절차 |
| [add-new-feature.md](./workflow/add-new-feature.md) | 새 기능 모듈 추가(레이어 역순 + 코드 템플릿 + 체크리스트) |

---

## 프로젝트 고유 요약

### NestJS ↔ WPF/MVVM 역할 매핑

| NestJS | ControlTowerWin | 책임 |
|--------|-----------------|------|
| `AppModule` | `Shell/` | 앱 호스트, 기능 조합 |
| `*.module.ts` | `Features/<기능>/` | 기능 경계 |
| `*.controller.ts` | `ViewModels/` | 입력 수신·위임 |
| `*.service.ts` | `Services/` | 비즈니스 로직(구현) |
| port / 추상 토큰 | `Interfaces/` | 계약(interface) 선언 |
| dto / entity | `Models/` | 데이터 형태 |
| `common`/`SharedModule` | `Shared/` | 횡단 공유 |

### 레이어 규약 요약

| 레이어 | 위치 | 네임스페이스 패턴 | 핵심 규칙 |
|--------|------|------------------|----------|
| Shell | `Shell/` | `ControlTowerWin.Shell.*` | 얇게(조합만), 로직 금지 |
| Models | `Features/<F>/Models/` | `...Features.<F>.Models` | 데이터만, 로직·UI 타입 금지 |
| Interfaces | `Features/<F>/Interfaces/` | `...Features.<F>.Interfaces` | 계약(interface)만, **항상 분리**(개수 무관) |
| Views | `Features/<F>/Views/` | `...Features.<F>.Views` | x:Class=폴더 경로, 코드비하인드 최소 |
| ViewModels | `Features/<F>/ViewModels/` | `...Features.<F>.ViewModels` | ViewModelBase 상속, 인터페이스 주입 |
| Services | `Features/<F>/Services/` | `...Features.<F>.Services` | 구현체, UI 무지향, GC 방지 강한 참조 |
| Shared | `Shared/` | `ControlTowerWin.Shared.*` | 단방향 의존(최하위) |

---

## 코드 리뷰 체크리스트

- [ ] 최상위는 `Shell / Features / Shared` 3분할만 유지하는가
- [ ] 새 기능은 `Features/<기능>/` 폴더에 자급자족으로 들어갔는가
- [ ] 파일 1개뿐인데 레이어 폴더를 강제하지 않았는가(점진적 승격) — 단, **인터페이스는 1개여도 `Interfaces/` 분리**
- [ ] 인터페이스(`I*`)를 `Services/`가 아니라 `Interfaces/`에 두고, Services엔 구현만 두었는가
- [ ] View 이동 시 `x:Class`와 코드비하인드 `namespace`를 폴더 경로에 맞춰 동시에 갱신했는가
- [ ] Model에 로직·WPF 표현 타입이 섞이지 않았는가
- [ ] ViewModel이 `ViewModelBase`를 상속하고 Service를 생성자로 주입받는가
- [ ] Service가 추적 중인 `Process`를 컬렉션에 강한 참조로 보관하는가(GC로 Exited 누락 방지)
- [ ] `Features/A`가 `Features/B`를 직접 참조하지 않는가(공유는 Shared 또는 Shell 경유)
- [ ] 주석이 `/* */`(C#)·`///`(XML 문서)로만 작성되고, 코드와 같은 줄(인라인) 주석이 없는가
- [ ] 빌드가 경고 0 / 오류 0으로 통과하는가
