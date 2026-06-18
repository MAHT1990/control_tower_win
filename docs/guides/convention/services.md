# Services 컨벤션

## 개요

**Services**는 실제 일을 하는 비즈니스 로직 레이어다. NestJS의 `*.service.ts`에 대응한다.
프로세스 추적, 터미널 실행, 커맨드 주입 등 "기능의 알맹이"가 여기 산다.

- 위치: `src/ControlTowerWin/Features/<기능>/Services/`
- 책임: OS·프로세스 제어, 데이터 수집/가공. UI를 모른다(이상적으로).
- 노출: ViewModel이 호출하거나, 이벤트로 결과를 통지

---

## 구조

```
Features/SessionMonitor/Services/ProcessTracker.cs     # PowerShell 세션 폴링·종료 감지
Features/TerminalLauncher/Services/TerminalLauncher.cs # wt 새 창 실행
Features/SendCommand/Services/StdinInjector.cs         # 세션에 커맨드 주입 (예정)
```

---

## 사용 패턴

### 현재 → 목표

현재 `MainWindow.xaml.cs`의 `OnTimerTick` 폴링과 `Process.Exited` 추적이 `ProcessTracker`로,
`OnNewTerminalClick`이 `TerminalLauncher`로 이동한다.

**Before (현재)** — Window가 직접 프로세스를 추적
```csharp
private void OnTimerTick(...)
{
    var current = PowerShellProcessNames.SelectMany(Process.GetProcessesByName)...;
    foreach (var p in current) { _trackedProcesses[p.Id] = p; Terminals.Add(...); p.Exited += ...; }
    var gone = _trackedProcesses.Keys.Except(currentPids);   // 폴링 이중 안전망
}
```

**After (목표)** — Service가 추적하고 이벤트로 통지
```csharp
public class ProcessTracker
{
    public event Action<TerminalInfo>? SessionAdded;
    public event Action<int>? SessionRemoved;      // pid
    private readonly Dictionary<int, Process> _tracked = new();   // 강한 참조(GC 방지)

    public void Start() { /* DispatcherTimer 또는 타이머로 폴링, 변화 시 이벤트 발화 */ }
}
```

VM은 이 이벤트만 구독한다 → UI와 추적 로직이 분리된다.

### 새 창 실행 (단순 Service)

```csharp
public class TerminalLauncher
{
    public void OpenNewWindow() =>
        Process.Start(new ProcessStartInfo { FileName = "wt", Arguments = "-w -1 nt", UseShellExecute = true });
}
```

### 인터페이스 분리 (교체 가능성이 있을 때)

SendCommand는 구현 방법이 여러 개(stdin / Win32 / Named Pipe)이므로 인터페이스로 추상화한다.

```csharp
public interface IStdinInjector { void Send(int pid, string command); }
public class StdinInjector : IStdinInjector { ... }   // 방법 A: RedirectStandardInput
```

---

## 주의사항

1. **GC 방지 강한 참조**: 추적 중인 `Process` 객체는 반드시 컬렉션(`Dictionary<int,Process>`)에
   보관한다. 지역 변수로 두면 GC가 수거해 `Exited` 이벤트가 발화되지 않는다(현재 코드의 핵심 교훈).
2. **이중 안전망 유지**: `Process.Exited`(즉시) + 폴링 비교(`Except`)의 이중 정리 전략을 그대로 가져온다.
   권한 부족 프로세스는 `EnableRaisingEvents`가 예외를 던질 수 있어 폴링이 백업한다.
3. **UI 스레드 책임 이전**: 현재 `Dispatcher.Invoke`는 Window가 했지만, Service는 UI를 모르는 게 이상적.
   Service는 그냥 이벤트를 쏘고, UI 스레드 전환은 구독자(ViewModel)가 담당한다.
4. **UseShellExecute=true**: `wt`는 Store 앱 실행 별칭이라 `UseShellExecute=true` 없이는
   "파일을 찾을 수 없음"이 난다. Launcher Service에서 이 옵션을 유지한다.
