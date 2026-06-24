# Interfaces 컨벤션

## 개요

**Interfaces**는 기능(또는 공유 영역)이 노출·소비하는 **계약(interface)** 만 모으는 전용 레이어다.
NestJS에서 포트(port)/추상 토큰을 한곳에 모으는 것과 같은 의도다. 구현(Services)과 계약(Interfaces)을
물리적으로 분리해, "무엇을 약속하는가"와 "어떻게 구현하는가"를 또렷이 가른다.

- 위치: `src/ControlTowerWin/Features/<기능>/Interfaces/` (기능별) · `src/ControlTowerWin/Shared/Core/Interfaces/` (공유)
- 책임: `interface`(및 필요 시 추상 계약 타입) 선언만. 구현 코드 금지.
- 네임스페이스: `ControlTowerWin.Features.<기능>.Interfaces`, `ControlTowerWin.Shared.Core.Interfaces`

> **핵심 규칙 — 항상 분리**: 인터페이스는 **파일이 1개여도** 무조건 `Interfaces/`에 둔다.
> (다른 레이어의 "파일 2개↑면 폴더 승격" 점진 규칙과 달리, 인터페이스는 개수와 무관하게 항상 분리한다.)
> 계약이 흩어지면 관리가 어렵기 때문이다.

---

## 구조

```
Features/SendCommand/
├── Interfaces/   IStdinInjector.cs        # 계약 (namespace ...Features.SendCommand.Interfaces)
├── Services/     StdinInjector.cs         # 구현 (IStdinInjector를 implements)
├── ViewModels/   SendCommandViewModel.cs  # 계약에 의존(생성자 주입)
└── Views/        SendCommandView.xaml(.cs)

Shared/
└── Core/
    ├── Interfaces/   (공유 계약 — 예: IDialogService.cs)
    ├── ViewModelBase.cs
    └── RelayCommand.cs
```

의존 방향:
```
ViewModels ──▶ Interfaces ◀── Services
            (계약에 의존)   (계약을 구현)
```
ViewModel은 **구현(Services)이 아니라 계약(Interfaces)에 의존**한다 → 교체·테스트가 쉬워진다.

---

## 사용 패턴

### 계약과 구현의 분리

```csharp
// Features/SendCommand/Interfaces/IStdinInjector.cs
namespace ControlTowerWin.Features.SendCommand.Interfaces;

public interface IStdinInjector
{
    int  LaunchControllableSession();
    bool CanSend(int pid);
    void Send(int pid, string command);
}
```
```csharp
// Features/SendCommand/Services/StdinInjector.cs
using ControlTowerWin.Features.SendCommand.Interfaces;   // ← 계약 참조

namespace ControlTowerWin.Features.SendCommand.Services;

public class StdinInjector : IStdinInjector { /* 구현 */ }
```

### ViewModel은 계약에 주입받는다

```csharp
// 구현이 아니라 인터페이스 타입으로 받는다 (DI 친화)
public SendCommandViewModel(int targetPid, IStdinInjector injector) { ... }
```

### 공유 계약은 Shared/Core/Interfaces

여러 기능이 공통으로 쓰는 계약(예: 다이얼로그 서비스)은 `Shared/Core/Interfaces/`에 둔다.

```csharp
// Shared/Core/Interfaces/IDialogService.cs
namespace ControlTowerWin.Shared.Core.Interfaces;
public interface IDialogService { bool? ShowDialog(object viewModel); }
```

### Good / Bad

**Good** — 계약은 Interfaces, 구현은 Services
```
Features/SendCommand/Interfaces/IStdinInjector.cs   # interface only
Features/SendCommand/Services/StdinInjector.cs      # class : IStdinInjector
```

**Bad** — 인터페이스를 Services에 섞어 둠
```
Features/SendCommand/Services/IStdinInjector.cs     # ← Interfaces/로 가야 함
Features/SendCommand/Services/StdinInjector.cs
```

**Bad** — 파일 1개라고 평면 배치
```
Features/SendCommand/IStdinInjector.cs              # ← 1개여도 Interfaces/로
```

---

## 주의사항

1. **선언만**: `Interfaces/`에는 `interface` 선언만 둔다. 구현·로직·필드는 Services의 책임.
2. **항상 분리(개수 무관)**: 점진적 승격 예외. 인터페이스는 1개여도 `Interfaces/`에 둔다.
3. **의존은 계약으로**: ViewModel·다른 레이어는 구현이 아니라 인터페이스 타입에 의존한다(생성자 주입).
4. **소속**: 한 기능 전용 계약은 그 기능의 `Interfaces/`, 여러 기능 공유 계약은 `Shared/Core/Interfaces/`.
5. **네임스페이스-경로 일치**: 폴더(`Interfaces`)와 네임스페이스 끝(`.Interfaces`)을 일치시킨다.
