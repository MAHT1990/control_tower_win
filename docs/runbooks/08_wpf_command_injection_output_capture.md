# Runbook 08 — 임베드 터미널: 명령 주입 + 출력 캡처

> **방식**: Runbook 06과 동일 — **직접 구현·검증(PoC) 후 검증된 절차를 런북으로 역산출**한다.
> 본 런북의 코드는 `.NET 10 / net10.0-windows`에서 빌드 0/0·실행 검증을 마친 것이다(커밋 `79ce6e9`).
> **🔤 C# 문법 짚기**: 각 Step 끝에 그 단계에서 새로 등장한 문법을 설명하는 박스를 둔다.

---

## 목표

Runbook 07의 다중 임베드 터미널에, 앱이 **프로그램적으로 명령을 주입**하고 **출력을 캡처**하는 기반을 더한다.

1. **앱 → 터미널 명령 주입**: 사람이 타이핑하지 않아도 앱이 커맨드를 보낸다(FR-014). 사람 타이핑과 **한 세션에서 공존**.
2. **터미널 출력 캡처**: 터미널이 뱉은 콘솔 텍스트를 앱이 읽는다(FR-002). 세션 간 출력 라우팅(11)의 소스.
3. 부가 UX: **커맨드 주입 바** · **pane 클릭 → 활성화/트리 동기** · **다중 대상 동시 주입**.

> 기획 근거: FR-014(임의 세션 커맨드 주입)·FR-002(입출력 파이프)·NFR-002(주입 ≤100ms)·NFR-018(엔진 경계).

---

## 전제 (사전 조건)

- [ ] Runbook 07 완료 — 탭>터미널 트리 + keep-alive pane 분할, `TerminalSessionsViewModel`/`TerminalSessionsView` 동작
- [ ] `Features/EmbeddedTerminal/`의 `TerminalView`(EasyTerminalControl 호스팅)·native 배포 정상
- [ ] `Shared/Core`의 `ViewModelBase`·`RelayCommand`

---

## 개념 (큰 틀)

```
              ┌─────────── 사람 타이핑 ───────────┐
              ▼                                   │
앱(VM) ─Inject─▶ EasyTerminalControl.ConPTYTerm ─▶ ConPTY ─▶ powershell
                       │  WriteToTerm(span)              │
                       └◀── GetConsoleText() ◀───────────┘
                            (LogConPTYOutput=true 로 누적)
```

### 엔진 경계 = `ITerminalSession` (NFR-018)

컨트롤의 저수준 API를 VM이 직접 만지지 않고, **주입/캡처를 계약 1곳으로 격리**한다. 엔진↔폴백 교체 시 VM 무변경.

### 실제 API (컨트롤에서 확정)

| 용도 | API | 위치 |
|------|-----|------|
| TermPTY 객체 접근 | `EasyTerminalControl.ConPTYTerm` | 컨트롤 |
| 명령 주입(텍스트) | `ConPTYTerm.WriteToTerm(ReadOnlySpan<char>)` | TermPTY |
| 출력 로깅 켜기 | `LogConPTYOutput = true` | **컨트롤** (⚠ TermPTY 아님) |
| 출력 텍스트 조회 | `ConPTYTerm.GetConsoleText()` | TermPTY |

> **함정 1**: `LogConPTYOutput`은 `TermPTY`가 아니라 **`EasyTerminalControl`(컨트롤)** 의 속성이다.
> TermPTY에 걸면 `CS1061`. (블루프린트 API 표가 이를 뭉뚱그렸던 지점.)
> **함정 2**: 캡처가 누적분을 담으려면 로깅을 **출력 발생 전(세션 생성 시)** 켜야 한다. 캡처 시점에 켜면 그 전 출력을 놓친다.
> **함정 3**(설계): 블루프린트가 상정한 `InterceptOutputToUITerminal(delegate)` 는 델리게이트 타입이 불명확하고 VT 변형 리스크가 있다. **on-demand `GetConsoleText()`** 가 단순·안전하며 캡처 버퍼(11)에 충분하다.

---

## 아키텍처 통합 (Feature × Layer)

```
Features/EmbeddedTerminal/
├── Interfaces/   ITerminalSession.cs        # (신규) 주입/캡처 계약 — 항상 분리 규칙
├── Services/     EasyTerminalSession.cs      # (신규) 컨트롤 래핑 구현
├── ViewModels/
│   ├── TerminalViewModel.cs                  # (수정) Session·Inject·CaptureOutput + IsInjectTarget
│   └── TerminalSessionsViewModel.cs          # (수정) CommandText·InjectCommand + 다중 대상
└── Views/
    ├── TerminalView.xaml(.cs)                 # (수정) 컨트롤 x:Name + Loaded에서 세션 주입
    └── TerminalSessionsView.xaml(.cs)         # (수정) 커맨드 바 + 체크박스 + pane 클릭 감지
```

---

## Step 1. ITerminalSession — 주입/캡처 계약

`Features/EmbeddedTerminal/Interfaces/ITerminalSession.cs` (신규)

```csharp
namespace ControlTowerWin.Features.EmbeddedTerminal.Interfaces;

/// <summary>
/// 임베드 터미널의 엔진 경계 계약 (NFR-018). 주입/캡처를 이 1곳으로 격리해
/// 엔진(EasyTerminalControl)↔폴백 교체 시 상위 소비자(ViewModel)가 무변경이 되게 한다.
/// </summary>
public interface ITerminalSession
{
    /* ConPTY가 기동되어 주입/캡처가 가능한 상태인지 */
    bool IsReady { get; }

    /* 입력 파이프에 텍스트 주입(FR-014). 개행(실행)은 호출자가 포함한다. */
    void Send(string text);

    /* 현재 콘솔 출력 캡처(FR-002). 세션 간 출력 라우팅(11)의 소스. */
    string GetOutputText();
}
```

### 🔤 C# 문법 짚기 (Step 1)

**인터페이스(interface) = 계약**
```csharp
public interface ITerminalSession { bool IsReady { get; } void Send(string text); ... }
```
- `interface`는 "이런 멤버를 가진다"는 **약속만** 선언하고 구현은 없다(메서드 몸통 `{}` 없음). 실제 동작은 이 인터페이스를 `implements`하는 클래스(Step 2)가 채운다.
- 왜 쓰나: VM은 "터미널에 보내고/읽는다"는 **약속**에만 의존하고, 그 뒤가 공식 WT 엔진이든 폴백이든 몰라도 된다(교체 자유).

---

## Step 2. EasyTerminalSession — 계약 구현 (컨트롤 래핑)

`Features/EmbeddedTerminal/Services/EasyTerminalSession.cs` (신규)

```csharp
using System;
using ControlTowerWin.Features.EmbeddedTerminal.Interfaces;
using EasyWindowsTerminalControl;

namespace ControlTowerWin.Features.EmbeddedTerminal.Services;

/// <summary>
/// EasyTerminalControl을 감싸 ITerminalSession(주입/캡처)을 실현하는 엔진 경계 구현.
/// 주입=ConPTYTerm.WriteToTerm, 캡처=LogConPTYOutput + GetConsoleText(on-demand).
/// ConPTYTerm은 프로세스 기동 전 null일 수 있어 매 호출 시 지연 접근한다.
/// </summary>
public class EasyTerminalSession : ITerminalSession
{
    private readonly EasyTerminalControl _control;

    public EasyTerminalSession(EasyTerminalControl control)
    {
        _control = control;
        /* 출력 누적 로깅을 세션 생성 시 조기 활성화 → 이후 GetConsoleText가 누적분을 반환.
           캡처 시점에 켜면 그 전 출력을 놓치므로 여기서 미리 켠다(함정 2). */
        _control.LogConPTYOutput = true;
    }

    public bool IsReady => _control.ConPTYTerm != null;

    /* 입력 파이프에 주입(사람 타이핑과 한 경로에 합류, NFR-002) */
    public void Send(string text)
    {
        var term = _control.ConPTYTerm;
        if (term is null || string.IsNullOrEmpty(text)) return;
        term.WriteToTerm(text.AsSpan());
    }

    /* 현재까지 누적된 콘솔 텍스트 캡처(관찰 전용, VT 변형 없음). */
    public string GetOutputText() => _control.ConPTYTerm?.GetConsoleText() ?? string.Empty;
}
```

### 🔤 C# 문법 짚기 (Step 2)

**① `ReadOnlySpan<char>` 와 `.AsSpan()`**
```csharp
term.WriteToTerm(text.AsSpan());
```
- `WriteToTerm`은 `ReadOnlySpan<char>`(문자열의 복사 없는 "구간 뷰")를 받는다. `text.AsSpan()`이 문자열을 그 뷰로 변환한다 — 새 배열을 만들지 않아 가볍다.

**② null 조건 `?.` + null 병합 `??`**
```csharp
_control.ConPTYTerm?.GetConsoleText() ?? string.Empty;
```
- `ConPTYTerm`이 null이면 `?.`가 호출을 건너뛰어 결과가 null → `??`가 그때 `string.Empty`(빈 문자열)로 대체. "터미널이 아직 없으면 빈 문자열" 한 줄 처리.

**③ 지연 접근 이유**
- `ConPTYTerm`을 생성자에서 캐싱하지 않고 **매번 `_control.ConPTYTerm`** 로 읽는다. 프로세스 기동 전엔 null이다가 이후 채워지므로, 호출 시점에 읽어야 최신 상태를 잡는다.

---

## Step 3. TerminalView — 컨트롤을 세션으로 감싸 VM에 주입

`Features/EmbeddedTerminal/Views/TerminalView.xaml` — 컨트롤에 `x:Name` 부여

```xml
<term:EasyTerminalControl x:Name="Term" StartupCommandLine="powershell.exe"/>
```

`Features/EmbeddedTerminal/Views/TerminalView.xaml.cs` — `Loaded`에서 세션 주입

```csharp
using System.Windows;
using System.Windows.Controls;
using ControlTowerWin.Features.EmbeddedTerminal.Services;
using ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

namespace ControlTowerWin.Features.EmbeddedTerminal.Views;

public partial class TerminalView : UserControl
{
    public TerminalView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /* View 로드 시 EasyTerminalControl을 감싼 세션을 VM에 주입(엔진 경계 연결).
       ConPTYTerm은 지연 접근이므로 여기서 컨트롤 참조만 넘겨도 충분하다. */
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TerminalViewModel vm)
        {
            vm.AttachSession(new EasyTerminalSession(Term));
        }
    }
}
```

### 🔤 C# 문법 짚기 (Step 3)

**VM ↔ 컨트롤 접근 경로 (MVVM 경계)**
- VM은 View 요소(`EasyTerminalControl`)를 직접 알면 안 된다. 그래서 **코드비하인드**가 다리를 놓는다: `Loaded` 시점에 컨트롤(`Term`)을 세션으로 감싸(`new EasyTerminalSession(Term)`) VM에 건넨다(`AttachSession`).
- `Loaded += OnLoaded`: View가 화면에 올라온 뒤 실행할 코드 등록(이벤트 구독). 그때는 `Term`이 준비돼 있다.

---

## Step 4. TerminalViewModel — 세션 보유·주입·캡처

`Features/EmbeddedTerminal/ViewModels/TerminalViewModel.cs` — 아래 멤버 추가 (07의 클래스 하단)

```csharp
using ControlTowerWin.Features.EmbeddedTerminal.Interfaces;
/* ... 기존 using ... */

    /* 엔진 경계 세션(주입/캡처). View 로드 시 EasyTerminalControl에서 주입됨(NFR-018). */
    private ITerminalSession? _session;

    public void AttachSession(ITerminalSession session) => _session = session;

    /* 커맨드 주입(FR-014/FN-SES-04). 개행을 붙여 실행한다. */
    public void Inject(string command)
    {
        if (_session is null || string.IsNullOrWhiteSpace(command)) return;
        _session.Send(command + "\r");
    }

    /* 현재 출력 캡처(FR-002 → 출력 라우팅 소스, 11) */
    public string CaptureOutput() => _session?.GetOutputText() ?? string.Empty;
```

- `Inject`가 `"\r"`(캐리지 리턴)을 붙여 **명령 실행**까지 시킨다(pwsh는 Enter로 제출).
- VM은 `ITerminalSession`(계약)에만 의존 — 구현(EasyTerminalSession)을 모른다.

---

## Step 5. 커맨드 주입 바 (선택 터미널에 주입)

`TerminalSessionsViewModel`에 입력 텍스트 + 주입 커맨드 추가

```csharp
using System;
using System.Linq;
/* ... */

    private string _commandText = string.Empty;
    public ICommand InjectCommand { get; }

    /* 컨텍스트 메뉴 "명령 실행"이 커맨드 바에 포커스를 요청 → View가 처리 (10에서 배선) */
    public event Action? FocusCommandBarRequested;

    /* 생성자에서: */
    // InjectCommand = new RelayCommand(_ => InjectToSelected(),
    //     _ => HasInjectTargets() && !string.IsNullOrWhiteSpace(CommandText));

    public string CommandText
    {
        get => _commandText;
        set { _commandText = value; OnPropertyChanged(); }
    }

    /* 주입 대상이 있는지: 체크된 다중 대상 OR 선택 터미널 */
    private bool HasInjectTargets() =>
        Tabs.Any(t => t.Terminals.Any(x => x.IsInjectTarget)) || SelectedTab?.SelectedTerminal != null;

    private void InjectToSelected()
    {
        InjectToTargets(CommandText);
        CommandText = string.Empty;
    }

    /* 텍스트를 대상 세션들에 독립 주입. 체크된 다중 대상 전부, 없으면 선택 터미널 하나. */
    private void InjectToTargets(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var targets = Tabs.SelectMany(t => t.Terminals).Where(t => t.IsInjectTarget).ToList();
        if (targets.Count == 0)
        {
            var single = SelectedTab?.SelectedTerminal;
            if (single != null) targets.Add(single);
        }
        foreach (var terminal in targets)
        {
            terminal.Inject(text);
        }
    }
```

`TerminalSessionsView.xaml` — 좌측 DockPanel 하단에 커맨드 바

```xml
<StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="0,4,0,0">
    <TextBox x:Name="CommandBox" Width="140"
             Text="{Binding CommandText, UpdateSourceTrigger=PropertyChanged}"
             KeyDown="CommandBox_KeyDown"/>
    <Button Content="실행" Command="{Binding InjectCommand}" Margin="4,0,0,0" Padding="6,2"/>
</StackPanel>
```

`TerminalSessionsView.xaml.cs` — Enter로 실행

```csharp
private void CommandBox_KeyDown(object sender, KeyEventArgs e)
{
    if (e.Key != Key.Enter) return;
    if (DataContext is TerminalSessionsViewModel vm && vm.InjectCommand.CanExecute(null))
    {
        vm.InjectCommand.Execute(null);
        e.Handled = true;
    }
}
```

### 🔤 C# 문법 짚기 (Step 5)

**① 이벤트(event) + 델리게이트 `Action`**
```csharp
public event Action? FocusCommandBarRequested;   // 선언
FocusCommandBarRequested?.Invoke();              // 발생(호출)
```
- `event Action?`은 "이 일이 일어나면 알림을 받겠다고 등록한 쪽에게 통지하는 방송국"이다. VM이 `?.Invoke()`로 방송하면, 구독한 View가 반응(포커스)한다. VM이 View를 직접 부르지 않고 **거꾸로 알림만** 보내 MVVM 경계를 지킨다.

**② LINQ `SelectMany` / `Where` / `Any`**
```csharp
Tabs.SelectMany(t => t.Terminals).Where(t => t.IsInjectTarget).ToList();
```
- `SelectMany`: 탭마다 터미널 목록을 꺼내 **하나의 평평한 목록**으로 합침(탭들의 모든 터미널). `Where(조건)`: 그중 체크된 것만. `ToList()`: 결과를 리스트로 굳힘. "모든 탭의 터미널 중 체크된 것 전부"를 한 줄로.

---

## Step 6. UX 보정 — pane 클릭 활성화 + 다중 대상 체크박스

### 6-1. pane 클릭 → 해당 터미널 활성화 + 좌측 트리 동기

`TerminalSessionsView.xaml` — pane Border에 포커스 감지

```xml
<Border Margin="2" BorderThickness="2" GotKeyboardFocus="Pane_GotFocus">
```

`TerminalSessionsView.xaml.cs`

```csharp
/* pane(터미널) 클릭·포커스 시 → 해당 터미널을 좌측 트리에서 선택.
   트리 선택이 OnTreeSelectionChanged를 통해 SelectedTerminal·pane 테두리·트리 하이라이트를 일괄 동기. */
private void Pane_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
{
    if (sender is FrameworkElement fe && fe.DataContext is TerminalViewModel terminal)
    {
        SelectTerminalInTree(terminal);
    }
}

/* 터미널에 해당하는 TreeViewItem을 프로그램적으로 선택(포커스는 터미널에 유지) */
private void SelectTerminalInTree(TerminalViewModel terminal)
{
    if (DataContext is not TerminalSessionsViewModel vm) return;
    var tab = vm.Tabs.FirstOrDefault(t => t.Terminals.Contains(terminal));
    if (tab is null) return;
    if (SessionTree.ItemContainerGenerator.ContainerFromItem(tab) is not TreeViewItem tabItem) return;
    tabItem.IsExpanded = true;
    if (tabItem.ItemContainerGenerator.ContainerFromItem(terminal) is TreeViewItem termItem
        && !termItem.IsSelected)
    {
        termItem.IsSelected = true;
    }
}
```

### 6-2. 다중 대상 체크박스

`TerminalViewModel`에 `IsInjectTarget`(bool 속성, `OnPropertyChanged` 포함) 추가.

`TerminalSessionsView.xaml` — 터미널 트리 노드 앞에 체크박스

```xml
<DataTemplate DataType="{x:Type vm:TerminalViewModel}">
    <StackPanel Orientation="Horizontal">
        <CheckBox IsChecked="{Binding IsInjectTarget}" VerticalAlignment="Center"
                  Margin="0,0,4,0" ToolTip="다중 주입 대상"/>
        <TextBlock Text="{Binding TreeLabel}"/>
    </StackPanel>
</DataTemplate>
```

### 🔤 C# 문법 짚기 (Step 6)

**① `GotKeyboardFocus` — HwndHost 포커스 감지**
- 터미널은 native 호스팅(HwndHost)이라 일반 WPF 마우스 이벤트가 안 잡힌다. 하지만 클릭하면 그 터미널이 **키보드 포커스**를 얻고, 그 이벤트는 감싼 Border까지 **버블링**된다. 그래서 `GotKeyboardFocus`로 "이 pane이 눌렸다"를 감지한다.

**② `ItemContainerGenerator.ContainerFromItem`**
```csharp
SessionTree.ItemContainerGenerator.ContainerFromItem(tab) is TreeViewItem tabItem
```
- 트리의 **데이터**(TabViewModel)에서 그걸 그리는 **UI 컨테이너**(TreeViewItem)를 되찾는다. 데이터↔화면요소 사이 다리. 여기서 `IsSelected=true`로 트리 선택을 코드로 옮긴다(선택 ≠ 포커스라 터미널 타이핑은 유지).

---

## Step 7. 빌드 및 실행 검증

```powershell
cd src/ControlTowerWin
dotnet build ControlTowerWin.csproj
```

1. 빌드 **경고 0 / 오류 0** (실행 중 인스턴스 있으면 exe 잠금 MSB3021 → 먼저 닫기)
2. 실행: `bin/Debug/net10.0-windows/win-x64/ControlTowerWin.exe`
3. 검증:
   - 터미널 선택 → 좌하단 바에 `Get-ChildItem` 입력 → **실행**(또는 Enter) → 그 터미널에서 실행
   - 주입 후 그 터미널에 **직접 타이핑도 정상**(사람 입력 공존)
   - **pane 클릭** → 그 터미널 파란 테두리 + 좌측 트리 선택 이동
   - 터미널 노드 **체크박스 여러 개 체크** → 커맨드 실행 → 체크된 전부에서 동시 실행

---

## 체크리스트

- [ ] Step 1: `Interfaces/ITerminalSession.cs`(IsReady·Send·GetOutputText)
- [ ] Step 2: `Services/EasyTerminalSession.cs` — 생성자에서 `LogConPTYOutput=true`(컨트롤!), `WriteToTerm`/`GetConsoleText`
- [ ] Step 3: `TerminalView` 컨트롤 `x:Name="Term"` + `Loaded`에서 `AttachSession`
- [ ] Step 4: `TerminalViewModel` — `AttachSession`·`Inject`(개행)·`CaptureOutput`
- [ ] Step 5: 커맨드 바 + `InjectCommand`/`InjectToTargets`
- [ ] Step 6: pane 클릭 `GotKeyboardFocus`·트리 동기 · 다중 대상 체크박스
- [ ] Step 7: 빌드 0/0 + 단일/다중 주입·pane 동기 실행 검증

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| `CS1061: TermPTY에 LogConPTYOutput 없음` | 로깅 속성을 TermPTY에 걸었음 | `_control.LogConPTYOutput`(컨트롤에) 로 이동 |
| 캡처가 빈 문자열/일부만 | 로깅을 캡처 시점에 켬 → 그 전 출력 유실 | 세션 **생성자**에서 `LogConPTYOutput=true` |
| 주입해도 실행 안 됨(줄만 입력) | 개행 미포함 | `Send(command + "\r")` |
| pane 클릭해도 트리 선택 안 옮겨짐 | HwndHost라 마우스 이벤트 안 잡힘 | `GotKeyboardFocus`로 감지(마우스 아님) |
| 빌드 MSB3021(exe 잠김) | 이전 앱 인스턴스 실행 중 | 앱 닫고 재빌드 |

---

## 다음 단계

- **09**: 세션 **재시작**(`RestartTerm`) + **종료·좀비 정리**(`DisconnectConPTYTerm`, `Unloaded`/앱 종료 훅).
- **10**: 세션 액션 **컨텍스트 메뉴**(SC-23, "명령 실행"이 이 커맨드 바에 포커스) + 인라인 **rename**.
- **11**: **출력 캡처 버퍼·라우팅**(FR-047) — `CaptureOutput()`을 소스로 A→B 출력 전달.

---

## 참고

- [Runbook 06 임베드 터미널](./06_wpf_embedded_terminal.md) · [Runbook 07 다중 탭](./07_wpf_multi_terminal_tabs.md)
- [interfaces.md](../guides/convention/interfaces.md) · [services.md](../guides/convention/services.md) · [viewmodels.md](../guides/convention/viewmodels.md)
- 기획: [`04_requirements`](../plans/v1/04_requirements.md)(FR-014·002·NFR-002·018) · [`10_tech`](../plans/v1/10_tech.md)(TS-04 주입/캡처)
- [EasyWindowsTerminalControl (GitHub)](https://github.com/mitchcapper/EasyWindowsTerminalControl) — ConPTYTerm / WriteToTerm / GetConsoleText
