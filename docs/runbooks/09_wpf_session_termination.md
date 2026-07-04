# Runbook 09 — 임베드 세션: 재시작 + 종료·좀비 정리

> **방식**: 직접 구현·검증 후 역산출. 코드는 빌드 0/0·실행 검증 완료(커밋 `0e55432`).
> **🔤 C# 문법 짚기**: 각 Step 끝에 새 문법 박스.

---

## 목표

앱-소유 임베드 세션(ConPTY)의 **수명**을 관리한다.

1. **재시작**(FN-SES-10): 세션을 깨끗한 새 ConPTY로 다시 띄운다(`RestartTerm`).
2. **종료·정리**(FN-TRM-02): 터미널을 닫거나 **앱을 종료할 때** ConPTY 자식(OpenConsole/pwsh)을 정리해 **좀비 프로세스**를 남기지 않는다.

> 기획 근거: FR-015(종료/강제종료)·FR-018(재시작)·FN-TRM-02(lifecycle 정리)·NFR-009(크래시 격리).
> 08의 `ITerminalSession` 엔진 경계에 수명 계약을 확장한다.

---

## 전제

- [ ] Runbook 08 완료 — `ITerminalSession`(IsReady·Send·GetOutputText)·`EasyTerminalSession`·세션 배선
- [ ] 07의 keep-alive pane 구조(탭 전환 시 세션 유지)

---

## 개념 (큰 틀)

```
[우클릭 재시작] ─▶ TerminalViewModel.Restart() ─▶ EasyTerminalControl.RestartTerm()
                                                    (기존 dispose + 새 pwsh 기동)

[터미널 닫힘 / 앱 종료] ─▶ Cleanup() ─▶ DisconnectConPTYTerm()  (자식 정리, 좀비 방지)
   ├─ TerminalView.Unloaded        (터미널 View가 트리에서 제거될 때)
   └─ MainWindow.Closing → CleanupAll()  (앱 종료 시 전 세션 일괄)
```

### keep-alive vs Unloaded (핵심 구분)

- 07의 keep-alive는 탭 전환 시 pane을 **Collapsed**(숨김)로 둘 뿐 트리에서 제거하지 않는다 → `Unloaded` **미발화** → 세션 유지.
- `Unloaded`는 View가 **실제로 트리에서 제거**될 때만(터미널 종료·앱 종료) 발화 → 그때 정리.
- 즉 "탭 전환엔 살아있고, 진짜 닫을 때만 정리"가 자동으로 성립한다.

> **주의**: `DisconnectConPTYTerm`이 자식 프로세스를 확실히 종료하는지는 런타임 검증 포인트(대량 반복 스트레스). 좀비가 남으면 컨트롤의 다른 정리(dispose/kill)로 보정.

---

## Step 1. ITerminalSession — 수명 계약 확장

`Interfaces/ITerminalSession.cs` — 08의 계약에 두 메서드 추가

```csharp
    /* 세션 재시작(FN-SES-10). 기존 ConPTY를 정리하고 새 프로세스를 기동한다. */
    void Restart();

    /* 세션 종료·정리(FN-TRM-02/FN-SES-05). ConPTY 자식을 정리해 좀비를 방지한다(멱등). */
    void Close();
```

---

## Step 2. EasyTerminalSession — 구현

`Services/EasyTerminalSession.cs` — 필드 + 두 메서드 추가

```csharp
    private bool _closed;

    /* 깨끗한 새 term으로 재시작(기존 dispose). StartupCommandLine 재적용. */
    public void Restart() => _control.RestartTerm();

    /* ConPTY 프론트엔드 분리·정리(좀비 방지). 멱등 — 중복 호출 무해. */
    public void Close()
    {
        if (_closed) return;
        _closed = true;
        _control.DisconnectConPTYTerm();
    }
```

### 🔤 C# 문법 짚기 (Step 2)

**멱등(idempotent) 가드 — `_closed` 플래그**
```csharp
if (_closed) return;
_closed = true;
```
- `Close()`가 여러 경로에서(View 언로드 + 앱 종료) 두 번 불릴 수 있다. 처음 호출에서 `_closed`를 켜고, 이후 호출은 맨 앞에서 되돌아간다 → **한 번만 실행**. 중복 정리로 인한 오류를 막는 흔한 패턴.

---

## Step 3. TerminalViewModel — 재시작·정리 위임

`ViewModels/TerminalViewModel.cs` — 하단에 추가

```csharp
    /* 세션 재시작(FN-SES-10) */
    public void Restart() => _session?.Restart();

    /* 세션 종료·정리(FN-TRM-02, 좀비 방지). View 언로드·앱 종료 시 호출. */
    public void Cleanup() => _session?.Close();
```

---

## Step 4. TerminalView — 언로드 시 정리 훅

`Views/TerminalView.xaml.cs` — 생성자에 `Unloaded` 구독 + 핸들러

```csharp
    public TerminalView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /* View가 트리에서 제거될 때(터미널 종료·앱 종료) ConPTY 정리(좀비 방지).
       keep-alive는 Collapsed일 뿐 언로드가 아니므로, 탭 전환에는 발화하지 않는다. */
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TerminalViewModel vm)
        {
            vm.Cleanup();
        }
    }
```

---

## Step 5. RestartCommand + 앱 종료 일괄 정리

`ViewModels/TerminalSessionsViewModel.cs`

```csharp
    /* 생성자: 재시작 커맨드(트리거는 10의 컨텍스트 메뉴 "재시작") */
    // RestartCommand = new RelayCommand(_ => SelectedTab?.SelectedTerminal?.Restart(),
    //                                   _ => SelectedTab?.SelectedTerminal != null);

    /* 앱 종료 시 소유 세션 일괄 정리(FN-TRM-02, 좀비 방지) */
    public void CleanupAll()
    {
        foreach (var terminal in Tabs.SelectMany(t => t.Terminals))
        {
            terminal.Cleanup();
        }
    }
```

`Shell/Views/MainWindow.xaml.cs` — `Closing`에서 일괄 정리

```csharp
using System.ComponentModel;
/* ... */

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Closing += OnClosing;
    }

    /* 앱 종료 시 앱-소유 ConPTY 세션 일괄 정리(FN-TRM-02, 좀비 방지) */
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.Sessions.CleanupAll();
        }
    }
```

### 🔤 C# 문법 짚기 (Step 5)

**`Closing` 이벤트 + `CancelEventArgs`**
```csharp
Closing += OnClosing;
private void OnClosing(object? sender, CancelEventArgs e) { ... }
```
- `Closing`은 창이 **닫히기 직전** 발화하는 이벤트. `CancelEventArgs.Cancel = true`로 닫힘을 취소할 수도 있다(여기선 안 씀). 닫히기 전에 세션 정리를 끼워 넣어 좀비를 막는다.
- `Unloaded`(Step 4)와 `Closing`(여기)의 이중 안전망: 앱 종료 시 `Closing`이 먼저 일괄 정리하고, 이어 각 View의 `Unloaded`도 정리 시도 → 멱등 `Close()`(Step 2)라 중복이 무해.

---

## Step 6. 빌드 및 실행 검증

```powershell
cd src/ControlTowerWin && dotnet build ControlTowerWin.csproj
```

1. 빌드 0/0 (실행 중 인스턴스 닫고)
2. 검증:
   - 우클릭 **재시작**(10의 메뉴) → 그 터미널이 새 pwsh로 재시작(화면/스크롤백 초기화)
   - 터미널 **닫기** → pane 사라짐 + 뒤의 pwsh 정리
   - **앱 종료**(창 X) 후 작업관리자에서 pwsh/OpenConsole **좀비 잔존 없음**
   - 탭 전환 시엔 세션 유지(keep-alive 그대로)

---

## 체크리스트

- [ ] Step 1: `ITerminalSession`에 `Restart()`·`Close()`
- [ ] Step 2: `EasyTerminalSession` — `RestartTerm()`·`DisconnectConPTYTerm()`(멱등 `_closed`)
- [ ] Step 3: `TerminalViewModel` — `Restart()`·`Cleanup()`
- [ ] Step 4: `TerminalView` `Unloaded` → `Cleanup()`
- [ ] Step 5: `RestartCommand` + `CleanupAll()` + `MainWindow.Closing`
- [ ] Step 6: 재시작·종료·앱종료 좀비 0 검증

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| 탭 전환마다 세션이 정리됨 | `Unloaded`가 탭 전환에 발화(가상화/재생성) | keep-alive(Collapsed) 유지 확인 — 제거가 아니어야 |
| 앱 종료 후 pwsh 좀비 잔존 | `DisconnectConPTYTerm`만으로 자식 미종료 | 컨트롤 dispose/프로세스 kill 보강(런타임 검증) |
| `Close` 중복 호출 오류 | `Unloaded`+`Closing` 이중 발화 | 멱등 `_closed` 가드(Step 2) |

---

## 다음 단계

- **10**: 컨텍스트 메뉴(SC-23) — "재시작"이 이 `RestartCommand`를, "종료"가 `CloseCommand`(07)를 호출.
- **11**: 출력 캡처 버퍼·라우팅.

---

## 참고

- [Runbook 08 명령주입·캡처](./08_wpf_command_injection_output_capture.md) · [Runbook 07 다중 탭](./07_wpf_multi_terminal_tabs.md)
- 기획: [`05_functions`](../plans/v1/05_functions.md)(FN-SES-05·06·10·FN-TRM-02) · [`10_tech`](../plans/v1/10_tech.md)(RISK-006 좀비 정리)
- [EasyWindowsTerminalControl](https://github.com/mitchcapper/EasyWindowsTerminalControl) — RestartTerm / DisconnectConPTYTerm
