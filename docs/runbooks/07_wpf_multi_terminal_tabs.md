# Runbook 07 — 탭>터미널 다중 임베드 + pane 분할 (레거시 정리 포함)

> **방식**: Runbook 06과 동일 — **직접 구현·검증(PoC)한 절차를 런북으로 역산출**한다.
> 본 런북의 코드는 `.NET 10 / net10.0-windows`에서 빌드 0/0·실행 검증을 마친 것이다.
> 학습을 위해 **런북을 보며 직접 손으로 타이핑**하는 것을 전제로 상세히 기술한다.
> **🔤 C# 문법 짚기**: C#/WPF가 처음이어도 따라올 수 있도록, 각 Step 끝에 그 단계에서 **새로 등장한 문법**만 골라 설명하는 박스를 둔다(앞 Step에서 설명한 건 반복하지 않는다).

---

## 목표

Runbook 06의 **단일** 임베드 터미널을, **탭 > 터미널 2계층 다중 임베드**로 확장한다.

- 여러 **탭**(워크스페이스)을 만들고, 각 탭이 **여러 터미널**을 소유한다.
- 좌측에 **트리**(탭 부모 → 터미널 자식, PID 표시), 탭을 고르면 그 탭의 터미널들이
  우측에 **pane 분할(타일)로 동시 표시**된다.
- **모든 탭·터미널의 ConPTY 세션은 탭을 전환해도 계속 살아있다**(keep-alive).
- 동시에, 기획과 어긋나는 **레거시 기능(전역 프로세스 감지·외부 WT 새창)을 폐기**한다.

> 기획 근거: FR-006(탭 컨테이너)·FR-008(탭 내 pane 분할)·FR-017(다중 세션 전환)·FR-039(앱-소유 세션 범위).

---

## 전제 (사전 조건)

- [ ] Runbook 06 완료 — `Features/EmbeddedTerminal/`의 `TerminalView`(EasyTerminalControl 호스팅) 동작,
      native(conpty.dll·OpenConsole.exe) 복사 포함 빌드 0/0.
- [ ] `Shared/Core`의 `ViewModelBase`·`RelayCommand` 존재(Runbook 04).
- [ ] 현재 `MainWindow`가 `SessionList`(SessionMonitor) + `Terminal`(단일) + `New Terminal Window` 버튼을 조합 중.

---

## 개념 (큰 틀)

### 1. 왜 레거시(SessionMonitor·TerminalLauncher)를 폐기하나

| 레거시 | 하던 일 | 폐기 이유 |
|--------|---------|-----------|
| `Features/SessionMonitor` (`ProcessTracker`) | 1초 폴링으로 **전역** pwsh/powershell 프로세스를 감지·목록화 | 앱이 spawn하지 않은 **외부** 프로세스를 관측 대상에 올림 → **FR-039 위반**(제어·관측은 앱-소유 세션 한정). ConPTY는 외부 프로세스 attach 불가라 어차피 제어도 못 함 |
| `Features/TerminalLauncher` (`wt -w -1 nt`) | 외부 **Windows Terminal 새 창**을 띄움 | 임베드 세션이 **유일한 터미널 실행 경로**여야 함(FR-039). 외부 새창은 v1 범위 밖 |

> 두 폴더를 통째로 삭제하고, 우리가 직접 spawn·소유하는 **앱-소유 세션 모델**만 남긴다.
> (삭제 코드는 git 이력에 남으므로 필요 시 참조 가능.)

### 2. 탭 > 터미널 2계층 모델

```
TerminalSessionsViewModel (최상위)
  └ Tabs : ObservableCollection<TabViewModel>
        └ TabViewModel (탭 = 워크스페이스)
              └ Terminals : ObservableCollection<TerminalViewModel>
                    └ TerminalViewModel (라이브 ConPTY 세션 1개 = pane 1개)
```

```
좌측 트리                        우측 (선택된 탭의 터미널 = pane 분할)
┌────────────────────┐   ┌──────────────┬──────────────┐
│ ▼ tab1             │   │ Terminal 1   │ Terminal 2   │
│    Terminal 1 (PID)│◀─┤ (pane)       │ (pane)       │
│    Terminal 2 (PID)│   ├──────────────┴──────────────┤
│ ▼ tab2             │   │ Terminal 3   (pane)         │
│    Terminal 1 (PID)│   └─────────────────────────────┘
└────────────────────┘      ← tab1의 터미널들만 타일로 동시 표시
```

### 3. keep-alive — **왜 `TabControl`을 안 쓰나** (이 런북의 핵심 함정)

WPF `TabControl`은 **`ContentPresenter`가 1개뿐**이라, 비활성 탭 콘텐츠를 **언로드/재생성**한다.
`EasyTerminalControl`은 native `HwndHost`라, View가 트리에서 제거되면 그 창이 파괴되고
**안에서 돌던 ConPTY(pwsh) 세션이 죽는다**. 탭을 왔다 갔다 할 때마다 세션이 재시작되면 못 쓴다.

그래서 **`ItemsControl` + Grid 겹침 + `Visibility` 토글**로 keep-alive를 만든다:

```
바깥 ItemsControl(ItemsSource = Tabs, 패널 = Grid)   ← 모든 탭을 같은 셀에 '겹쳐' 생성
   각 탭 컨테이너 Visibility = IsSelected ? Visible : Collapsed
      안쪽 ItemsControl(ItemsSource = Terminals, 패널 = UniformGrid)  ← 그 탭의 터미널을 타일
         각 터미널 = TerminalView(EasyTerminalControl)

핵심: ItemsControl은 항목 View를 '재활용/파괴'하지 않는다.
     → 비선택 탭·비표시 터미널의 View도 살아남아 ConPTY 유지.
     → Collapsed는 hwnd를 '숨김'일 뿐 파괴가 아니므로 세션 생존.
```

### 4. airspace / 트리 템플릿 충돌 주의

- **airspace**: native HwndHost 위에는 WPF를 겹칠 수 없다. 하지만 **타일은 서로 겹치지 않으므로 안전**하고,
  포커스 강조 **테두리(Border)** 는 터미널 native 영역 *바깥*이라 문제없다(오버레이 아님).
- **템플릿 충돌**: `TerminalViewModel`은 **좌측 트리 노드**로도, **우측 pane**으로도 쓰인다.
  암시적(`DataType`) `DataTemplate` 하나로 두면 트리에도 터미널이 통째로 렌더된다 → 잘못.
  → 트리용은 `HierarchicalDataTemplate.ItemTemplate`에 **가둬** 텍스트로, 우측용은 **키드(`x:Key`) 템플릿**으로 분리한다.
- **TreeView 선택**: `TreeView.SelectedItem`은 **읽기 전용**이라 바인딩 불가 → 코드비하인드 `SelectedItemChanged`로 VM에 반영한다.

---

## 아키텍처 통합 (Feature × Layer)

```
Features/EmbeddedTerminal/
├── ViewModels/
│   ├── TerminalViewModel.cs         # (수정) Title + IsActive(포커스) + Pid + TreeLabel
│   ├── TabViewModel.cs              # (신규) 탭 = 터미널 컬렉션 소유
│   └── TerminalSessionsViewModel.cs # (신규) 최상위 = 탭 컬렉션 소유·추가·닫기
└── Views/
    ├── TerminalView.xaml(.cs)       # (06 그대로) EasyTerminalControl 호스팅
    └── TerminalSessionsView.xaml(.cs) # (신규) 좌 트리 + 우 keep-alive 분할

Shell/
├── ViewModels/ MainWindowViewModel.cs  # (수정) TerminalSessionsViewModel만 보유
└── Views/      MainWindow.xaml          # (수정) 단일 ContentControl 조합
```

폐기: `Features/SessionMonitor/` · `Features/TerminalLauncher/` (통째 삭제).

---

## Step 1. 레거시 Feature 제거

`Features/SessionMonitor/`와 `Features/TerminalLauncher/` **폴더 전체를 삭제**한다.

```powershell
cd src/ControlTowerWin
git rm -r Features/SessionMonitor Features/TerminalLauncher
```

> Shell(`MainWindowViewModel`)이 이들을 `new` 하고 있으므로 **지금은 빌드가 깨진다**. Step 6에서 조합부를 교체하며 해소된다.

---

## Step 2. TerminalViewModel — 포커스·PID 추가

`Features/EmbeddedTerminal/ViewModels/TerminalViewModel.cs` (06의 빈 앵커를 아래로 교체)

```csharp
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

/// <summary>
/// 하나의 임베드 터미널(라이브 ConPTY 세션)을 표현하는 ViewModel.
/// 탭에 귀속되어 좌측 트리에 자식 노드(PID 포함)로, 우측엔 pane으로 표시된다.
/// IsActive는 탭 내 포커스된 pane을 나타내어 테두리로 강조된다.
/// 프로그램적 명령 주입(WriteToTerm)·출력 가로채기는 Runbook 08에서 이 VM에 추가한다.
/// </summary>
public class TerminalViewModel : ViewModelBase
{
    private bool _isActive;
    private int _pid;

    public string Title { get; }

    public TerminalViewModel(string title) => Title = title;

    /* 포커스된 pane(테두리 강조 대상) */
    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    /* ConPTY 자식(pwsh) 프로세스 ID. 컨트롤 기동 후 채워진다(0=미확정) */
    public int Pid
    {
        get => _pid;
        set { _pid = value; OnPropertyChanged(); OnPropertyChanged(nameof(TreeLabel)); }
    }

    /* 좌측 트리 표시 라벨 */
    public string TreeLabel => _pid > 0 ? $"{Title} (PID {_pid})" : $"{Title} (PID …)";
}
```

- `IsActive`: 좌측 트리에서 고른 터미널을 우측 pane 테두리로 강조하기 위한 플래그.
- `Pid`/`TreeLabel`: 트리에 `Terminal 1 (PID 12345)`처럼 표시. **PID 실값 연결은 후속**이라 지금은 `(PID …)`로 뜬다(구조만 준비).

### 🔤 C# 문법 짚기 (Step 2)

C#이 처음이라면 이 파일에 쓰인 문법을 아래에서 짚어 둔다.

**① 필드(field) vs 속성(property)**
```csharp
private bool _isActive;              /* 필드: 값을 실제로 담는 내부 저장소. 앞의 _는 "private 필드" 관례 */
public bool IsActive { get; set; }   /* 속성: 바깥에서 값을 읽고/쓰는 창구 */
```
- 필드는 클래스 **안에서만** 쓰는 실제 상자, 속성은 바깥에 낸 **창구**다. WPF 바인딩(`{Binding IsActive}`)은 속성만 본다.

**② 자동 속성 (get만 = 읽기 전용)**
```csharp
public string Title { get; }   /* get만 있으면 읽기 전용. 생성자에서 한 번 정해지면 못 바꿈 */
```

**③ 전체 속성 (get/set + 변경 통지)**
```csharp
public bool IsActive
{
    get => _isActive;                               /* 읽을 때: 필드 값을 돌려줌 */
    set { _isActive = value; OnPropertyChanged(); } /* 쓸 때: 필드에 저장 + 화면에 "바뀌었다" 통지 */
}
```
- `value`는 C# **예약어**로, `set`에 들어온 새 값을 가리킨다(`IsActive = true` 하면 `value == true`).
- `OnPropertyChanged()`는 `ViewModelBase`가 준 메서드로, WPF에 "이 속성 바뀌었으니 화면 갱신해"라고 알린다. **이게 없으면 값은 바뀌어도 화면이 안 바뀐다.**

**④ `=>` (expression-bodied, 식 본문)**
```csharp
public TerminalViewModel(string title) => Title = title;   /* 생성자를 한 줄로 */
get => _isActive;                                          /* getter를 한 줄로 */
```
- `=> 식`은 `{ return 식; }`(또는 문장 하나)의 짧은 표기다. 몸통이 한 줄이면 중괄호 대신 쓴다.

**⑤ 문자열 보간 `$"..."` · 삼항 `?:` · `nameof`**
```csharp
public string TreeLabel => _pid > 0 ? $"{Title} (PID {_pid})" : $"{Title} (PID …)";
```
- `$"..."` 안의 `{Title}`은 그 자리에 변수 값을 끼워 넣는다(보간).
- `조건 ? A : B`는 "조건이 참이면 A, 아니면 B"인 **삼항 연산자**다. `_pid`가 0보다 크면 실제 PID를, 아니면 `(PID …)`를 만든다.
- `OnPropertyChanged(nameof(TreeLabel))`의 `nameof(TreeLabel)`은 문자열 `"TreeLabel"`을 **오타 없이** 만들어 준다(속성 이름을 바꾸면 자동 반영). `Pid`가 바뀌면 `TreeLabel` 표시도 갱신하라고 함께 통지하는 것.

---

## Step 3. TabViewModel — 탭(터미널 컬렉션 소유)

`Features/EmbeddedTerminal/ViewModels/TabViewModel.cs` (신규)

```csharp
using System.Collections.ObjectModel;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

/// <summary>
/// 하나의 탭(워크스페이스). 여러 터미널(pane)을 소유하며, 선택된 탭의 터미널들이
/// 우측에 pane 분할로 동시 표시된다(FR-006 탭 + FR-008 탭 내 분할).
/// 비선택 탭의 터미널도 View는 살아남아 ConPTY 세션이 유지된다(keep-alive).
/// </summary>
public class TabViewModel : ViewModelBase
{
    private bool _isSelected;
    private TerminalViewModel? _selectedTerminal;
    private int _counter;

    public string Title { get; }

    public ObservableCollection<TerminalViewModel> Terminals { get; } = new();

    public TabViewModel(string title)
    {
        Title = title;
        AddTerminal();
    }

    /* 현재 화면에 표시되는 탭인지(우측 pane 영역 노출 토글) */
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    /* 탭 내 포커스된 터미널(pane 테두리 강조) */
    public TerminalViewModel? SelectedTerminal
    {
        get => _selectedTerminal;
        set
        {
            _selectedTerminal = value;
            OnPropertyChanged();
            foreach (var t in Terminals)
                t.IsActive = ReferenceEquals(t, value);
        }
    }

    public TerminalViewModel AddTerminal()
    {
        var terminal = new TerminalViewModel($"Terminal {++_counter}");
        Terminals.Add(terminal);
        SelectedTerminal = terminal;
        return terminal;
    }

    public void RemoveTerminal(TerminalViewModel terminal)
    {
        var idx = Terminals.IndexOf(terminal);
        if (idx < 0) return;
        Terminals.Remove(terminal);
        SelectedTerminal = Terminals.Count == 0
            ? null
            : Terminals[System.Math.Min(idx, Terminals.Count - 1)];
    }
}
```

- `IsSelected`: 우측에서 **이 탭이 보이는지** 토글(바깥 ItemsControl의 `Visibility` 바인딩 대상).
- `SelectedTerminal`: 탭 내부의 **포커스 pane**. 세팅 시 소속 터미널들의 `IsActive`를 갱신.
- 탭을 새로 만들면 터미널 1개를 자동 생성(빈 탭 방지).

### 🔤 C# 문법 짚기 (Step 3)

**① `using` 지시문**
```csharp
using System.Collections.ObjectModel;   /* ObservableCollection이 든 "도구상자"를 가져옴 */
```
- 파일 맨 위 `using`은 "이 파일에서 이 네임스페이스의 타입을 짧은 이름으로 쓰겠다"는 선언이다.

**② 제네릭 컬렉션 `<T>` + ObservableCollection**
```csharp
public ObservableCollection<TerminalViewModel> Terminals { get; } = new();
```
- `<TerminalViewModel>`은 "이 목록엔 TerminalViewModel만 담는다"는 **제네릭 타입 인자**다(형이 고정돼 안전).
- `ObservableCollection`은 일반 리스트와 달리 **항목이 추가/삭제되면 화면에 자동 통지**한다(WPF 목록 바인딩용). Step 2 ③의 속성 통지를 목록 버전으로 해 주는 셈.
- `= new();`의 `new()`는 **타입을 생략한 생성**이다. 왼쪽에 이미 타입이 있으니 `new ObservableCollection<TerminalViewModel>()`를 `new()`로 줄였다.

**③ nullable 참조 타입 `T?`**
```csharp
private TerminalViewModel? _selectedTerminal;   /* ? = "이 변수는 null(비어 있음)일 수 있다" */
```
- 타입 뒤 `?`는 "값이 없을 수도 있음"을 명시한다. 선택된 터미널이 아직 없을 수 있어 `?`를 붙였다(이 프로젝트는 `<Nullable>enable</Nullable>`이라 `?` 없는 타입에 null을 넣으면 경고가 뜬다).

**④ `foreach` + `var` + `ReferenceEquals`**
```csharp
foreach (var t in Terminals)
    t.IsActive = ReferenceEquals(t, value);
```
- `foreach (var t in 목록)`은 목록을 하나씩 꺼내 `t`로 반복한다. `var`는 타입을 컴파일러가 추론(`t`는 TerminalViewModel).
- `ReferenceEquals(a, b)`는 두 변수가 **똑같은 객체**를 가리키는지 검사한다. 즉 "지금 도는 터미널이 방금 선택된 그 터미널이면 IsActive=true" → 하나만 활성 표시된다.

**⑤ 메서드 반환형 · `IndexOf` · `Math.Min`**
```csharp
public TerminalViewModel AddTerminal() { ... return terminal; }  /* 반환형 TerminalViewModel = 만든 걸 돌려줌 */
var idx = Terminals.IndexOf(terminal);                           /* 목록에서 몇 번째인지(없으면 -1) */
Terminals[System.Math.Min(idx, Terminals.Count - 1)]             /* 둘 중 작은 값 = 범위 넘침 방지 */
```
- 메서드 이름 앞의 타입(`TerminalViewModel`)이 **반환형**이다. `void`면 "돌려주는 값 없음".
- `System.Math.Min`처럼 **정규화된 이름**(네임스페이스.타입.메서드)을 쓰면 `using` 없이도 바로 호출된다. 삭제한 터미널 다음으로 어떤 걸 선택할지 고를 때 인덱스가 목록 밖으로 안 나가게 막는 계산이다.

---

## Step 4. TerminalSessionsViewModel — 최상위(탭 관리)

`Features/EmbeddedTerminal/ViewModels/TerminalSessionsViewModel.cs` (신규)

```csharp
using System.Collections.ObjectModel;
using System.Windows.Input;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

/// <summary>
/// 앱-소유 임베드 터미널의 최상위 관리자 (FR-006/008/017, L0).
/// 탭(워크스페이스)들을 소유하고, 선택된 탭의 터미널들을 우측에 pane 분할로 표시한다.
/// 모든 탭·터미널 View는 살아있는 채(keep-alive) 유지된다.
/// </summary>
public class TerminalSessionsViewModel : ViewModelBase
{
    private TabViewModel? _selectedTab;
    private int _counter;

    public ObservableCollection<TabViewModel> Tabs { get; } = new();
    public ICommand AddTabCommand { get; }
    public ICommand AddTerminalCommand { get; }
    public ICommand CloseCommand { get; }

    public TerminalSessionsViewModel()
    {
        AddTabCommand = new RelayCommand(_ => AddTab());
        AddTerminalCommand = new RelayCommand(_ => SelectedTab?.AddTerminal(), _ => SelectedTab != null);
        CloseCommand = new RelayCommand(_ => CloseSelected(), _ => SelectedTab != null);
        AddTab();
    }

    /* 화면에 표시되는 탭(우측 pane 영역 노출 토글) */
    public TabViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            _selectedTab = value;
            OnPropertyChanged();
            foreach (var tab in Tabs)
                tab.IsSelected = ReferenceEquals(tab, value);
        }
    }

    private void AddTab()
    {
        var tab = new TabViewModel($"tab{++_counter}");
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    /* 포커스 터미널이 있으면 그 터미널만, 없으면 탭 전체를 닫는다 */
    private void CloseSelected()
    {
        if (SelectedTab is null) return;

        var focused = SelectedTab.SelectedTerminal;
        if (focused != null && SelectedTab.Terminals.Count > 1)
        {
            SelectedTab.RemoveTerminal(focused);
            return;
        }

        var idx = Tabs.IndexOf(SelectedTab);
        Tabs.Remove(SelectedTab);
        SelectedTab = Tabs.Count == 0
            ? null
            : Tabs[System.Math.Min(idx, Tabs.Count - 1)];
    }
}
```

- `AddTabCommand` → 새 탭(+터미널 1). `AddTerminalCommand` → **선택된 탭에** 터미널 추가. `CloseCommand` → 포커스 터미널이 2개 이상 중 하나면 그 터미널만, 아니면 탭 전체를 닫음.
- `SelectedTab` 세팅 시 모든 탭의 `IsSelected` 갱신 → 우측에서 **선택 탭만** 보이게 된다.

### 🔤 C# 문법 짚기 (Step 4)

**① `ICommand` + `RelayCommand` (버튼 → 메서드 연결)**
```csharp
public ICommand AddTabCommand { get; }
AddTabCommand = new RelayCommand(_ => AddTab());
```
- WPF 버튼의 `Command="{Binding AddTabCommand}"`는 `ICommand` 타입에 연결된다. `RelayCommand`(Shared/Core)는 그 간편 구현으로, "누르면 실행할 동작"을 넣어 만든다(이벤트 핸들러 `Click=` 대신 쓰는 MVVM 방식).

**② 람다식 `=>` 와 버림 매개변수 `_`**
```csharp
_ => AddTab()                                              /* "인자는 안 쓰고, 그냥 AddTab() 실행" */
_ => SelectedTab?.AddTerminal(), _ => SelectedTab != null  /* 실행 동작 , 실행 가능 여부 */
```
- `매개변수 => 식`은 이름 없는 짧은 함수(**람다**)다. `RelayCommand`가 나중에 실행할 코드를 이렇게 건넨다(Step 2 ④의 `=>`와 같은 기호지만, 여기선 "함수 자체를 값으로" 넘긴다).
- `_`(밑줄)는 **"이 인자는 안 쓴다"**는 표시(버림, discard). Command는 파라미터를 넘길 수 있지만 여기선 안 쓰므로 `_`.
- `RelayCommand`의 **두 번째 인자** `_ => SelectedTab != null`은 **실행 가능 여부**(canExecute)다. 이게 false면 버튼이 자동으로 비활성(회색)된다.

**③ null 조건 연산자 `?.`**
```csharp
SelectedTab?.AddTerminal()   /* SelectedTab이 null이면 아무것도 안 하고 넘어감 */
```
- `?.`는 "앞이 null이 아닐 때만 뒤를 실행"한다. null인데 `.AddTerminal()`을 부르면 프로그램이 터지는데(NullReferenceException), `?.`가 그걸 막는다.

**④ `is null` 가드**
```csharp
if (SelectedTab is null) return;   /* SelectedTab이 비었으면 즉시 함수 종료 */
```
- `x is null`은 "x가 null이냐"를 묻는 현대적 표기(= `x == null`). 함수 앞머리에서 예외 상황을 먼저 걸러 내고 빠져나가는 **가드(guard)** 패턴이다.

---

## Step 5. TerminalSessionsView — 좌 트리 + 우 keep-alive 분할

> `TerminalView.xaml(.cs)`은 **06 그대로 재사용**한다(EasyTerminalControl 호스팅). 새로 만들지 않는다.

`Features/EmbeddedTerminal/Views/TerminalSessionsView.xaml` (신규)

```xml
<UserControl x:Class="ControlTowerWin.Features.EmbeddedTerminal.Views.TerminalSessionsView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:ControlTowerWin.Features.EmbeddedTerminal.ViewModels"
             xmlns:local="clr-namespace:ControlTowerWin.Features.EmbeddedTerminal.Views">
    <UserControl.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>

        <!-- 좌측 트리: 탭(부모) → 터미널(자식, PID 표시) -->
        <HierarchicalDataTemplate DataType="{x:Type vm:TabViewModel}"
                                  ItemsSource="{Binding Terminals}">
            <TextBlock Text="{Binding Title}" FontWeight="Bold"/>
            <HierarchicalDataTemplate.ItemTemplate>
                <DataTemplate DataType="{x:Type vm:TerminalViewModel}">
                    <TextBlock Text="{Binding TreeLabel}"/>
                </DataTemplate>
            </HierarchicalDataTemplate.ItemTemplate>
        </HierarchicalDataTemplate>

        <!-- 우측 pane: 라이브 터미널. 테두리는 native 영역 밖이라 airspace 무관. 활성 pane만 강조 -->
        <DataTemplate x:Key="TerminalPane" DataType="{x:Type vm:TerminalViewModel}">
            <Border Margin="2" BorderThickness="2">
                <Border.Style>
                    <Style TargetType="Border">
                        <!-- 기본값은 로컬 속성이 아니라 스타일 Setter로! (로컬 값은 트리거를 이겨 버림 — 아래 문법 박스 ⑥) -->
                        <Setter Property="BorderBrush" Value="Transparent"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding IsActive}" Value="True">
                                <Setter Property="BorderBrush" Value="#3B82F6"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </Border.Style>
                <local:TerminalView/>
            </Border>
        </DataTemplate>
    </UserControl.Resources>

    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="220"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- 좌측: 탭>터미널 트리 + 조작 -->
        <DockPanel Grid.Column="0">
            <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="0,0,0,4">
                <Button Content="새 탭" Command="{Binding AddTabCommand}" Margin="0,0,4,0" Padding="6,2"/>
                <Button Content="새 터미널" Command="{Binding AddTerminalCommand}" Margin="0,0,4,0" Padding="6,2"/>
                <Button Content="닫기" Command="{Binding CloseCommand}" Padding="6,2"/>
            </StackPanel>
            <TreeView x:Name="SessionTree" ItemsSource="{Binding Tabs}"
                      SelectedItemChanged="OnTreeSelectionChanged">
                <TreeView.ItemContainerStyle>
                    <Style TargetType="TreeViewItem">
                        <Setter Property="IsExpanded" Value="True"/>
                    </Style>
                </TreeView.ItemContainerStyle>
            </TreeView>
        </DockPanel>

        <!-- 우측: 탭들을 한 셀에 겹쳐 살려두고(keep-alive) 선택된 탭만 표시.
             선택된 탭 안에서 터미널들을 UniformGrid로 pane 분할 동시 표시(FR-006 탭 + FR-008 분할). -->
        <ItemsControl Grid.Column="1" Margin="8,0,0,0" ItemsSource="{Binding Tabs}">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                    <Grid/>
                </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.ItemContainerStyle>
                <Style TargetType="ContentPresenter">
                    <Setter Property="Visibility"
                            Value="{Binding IsSelected, Converter={StaticResource BoolToVis}}"/>
                </Style>
            </ItemsControl.ItemContainerStyle>
            <ItemsControl.ItemTemplate>
                <DataTemplate DataType="{x:Type vm:TabViewModel}">
                    <ItemsControl ItemsSource="{Binding Terminals}"
                                  ItemTemplate="{StaticResource TerminalPane}">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <UniformGrid/>
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                    </ItemsControl>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </Grid>
</UserControl>
```

### 왜 이렇게 짜는가 (한 줄씩)

- **`HierarchicalDataTemplate`**(탭) + 그 안의 `ItemTemplate`(터미널=텍스트): 트리 계층을 만들되,
  터미널 노드는 **텍스트로만** 그린다. 이 터미널 템플릿은 트리 안에 **갇혀** 있어 우측 pane과 충돌하지 않는다.
- **`x:Key="TerminalPane"`**: 우측 전용 템플릿(테두리 + `TerminalView`). 키가 있으므로 암시적으로 트리에 새지 않는다.
- **바깥 `ItemsControl`(패널=Grid)** + `ContentPresenter.Visibility = IsSelected`: 모든 탭을 겹쳐 살려두고 선택 탭만 표시.
- **안쪽 `ItemsControl`(패널=UniformGrid)**: 선택 탭의 터미널들을 자동 타일(1→1, 2→좌우, 3~4→2×2…).
- `IsExpanded=True`: 트리의 탭 노드를 펼쳐 터미널이 바로 보이게.

`Features/EmbeddedTerminal/Views/TerminalSessionsView.xaml.cs` (신규 — 트리 선택 반영)

```csharp
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

namespace ControlTowerWin.Features.EmbeddedTerminal.Views;

public partial class TerminalSessionsView : UserControl
{
    public TerminalSessionsView() => InitializeComponent();

    /* TreeView.SelectedItem은 읽기전용이라 바인딩 불가 → 코드비하인드에서 VM 선택 상태로 반영 */
    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not TerminalSessionsViewModel vm) return;

        switch (e.NewValue)
        {
            case TabViewModel tab:
                vm.SelectedTab = tab;
                break;
            case TerminalViewModel terminal:
                var parent = vm.Tabs.FirstOrDefault(t => t.Terminals.Contains(terminal));
                if (parent is not null)
                {
                    vm.SelectedTab = parent;
                    parent.SelectedTerminal = terminal;
                }
                break;
        }
    }
}
```

- 탭 노드 선택 → 그 탭을 표시. 터미널 노드 선택 → 소속 탭을 찾아 표시 + 그 터미널을 포커스(테두리).

### 🔤 C# 문법 짚기 (Step 5)

XAML(화면 배치)의 짜임새는 위 "왜 이렇게 짜는가"에서 다뤘고, 여기선 **코드비하인드(.xaml.cs)의 C# 문법**과 **XAML 바인딩 기초**를 짚는다.

**① `partial class` (XAML ↔ 코드비하인드 한 쌍)**
```csharp
public partial class TerminalSessionsView : UserControl { ... }
```
- `partial`은 "이 클래스는 여러 파일로 나뉘어 있다"는 뜻. `.xaml`이 자동 생성하는 반쪽과 `.xaml.cs`의 반쪽이 합쳐져 하나의 클래스가 된다. `InitializeComponent()`가 그 XAML 반쪽을 불러온다.
- `: UserControl`은 **상속**이다. "이 클래스는 UserControl을 물려받는다" → 화면 조각으로 쓸 수 있게 됨.

**② 타입 패턴 `is not T x` (형 검사 + 변수 받기)**
```csharp
if (DataContext is not TerminalSessionsViewModel vm) return;
```
- "DataContext가 TerminalSessionsViewModel **타입이 아니면** 함수 종료"이고, **맞으면** 그 값을 `vm`이라는 변수에 담아 아래에서 바로 쓴다(형 검사 + 형변환 + 변수 선언을 한 방에).

**③ `switch` + 타입 패턴 매칭**
```csharp
switch (e.NewValue)
{
    case TabViewModel tab:        /* 선택된 게 탭이면, 그걸 tab으로 받아 */
        vm.SelectedTab = tab;
        break;                    /* 이 갈래 끝 (다음 case로 안 흘러감) */
    case TerminalViewModel terminal:
        ...
        break;
}
```
- `switch`는 값에 따라 갈래를 고른다. `case 타입 변수:`는 "이 타입이면 이 변수로 받아 처리". C#은 각 갈래 끝에 `break;`가 **필수**다(안 쓰면 컴파일 오류).

**④ LINQ: `FirstOrDefault` + 람다 + `Contains`**
```csharp
var parent = vm.Tabs.FirstOrDefault(t => t.Terminals.Contains(terminal));
```
- `FirstOrDefault(조건)`은 목록에서 **조건을 처음 만족하는 항목**을 돌려주고, 없으면 null을 준다(`using System.Linq` 필요).
- `t => t.Terminals.Contains(terminal)`은 "그 탭의 터미널 목록에 이 터미널이 들어 있냐"는 조건 람다 → 이 터미널의 **부모 탭**을 찾는다.
- `parent is not null`은 "찾았으면"이라는 뜻(②의 `not` 패턴).

**⑤ XAML 바인딩 기초 (참고)**
- `{Binding Tabs}` — 화면 요소를 VM의 `Tabs` 속성에 연결. VM에서 값이 바뀌면(=OnPropertyChanged/ObservableCollection) 화면이 자동 갱신.
- `{StaticResource BoolToVis}` — 위 `<UserControl.Resources>`에 `x:Key="BoolToVis"`로 등록해 둔 변환기를 가져다 씀(bool → Visibility 변환).
- `Command="{Binding AddTabCommand}"` — 버튼 클릭을 VM의 `ICommand`에 연결(Step 4 참조).
- **왜 코드비하인드를 쓰나**: `TreeView.SelectedItem`은 읽기 전용이라 `{Binding}`으로 못 묶는다 → 예외적으로 `.xaml.cs`에서 이벤트(`SelectedItemChanged`)로 VM에 값을 넘긴다(위 개념 §4).

**⑥ WPF 속성 우선순위 함정 ★ (파란 테두리가 안 뜰 때)**
```xml
<!-- 잘못: BorderBrush를 요소에 직접(로컬 값) → 트리거가 못 이김 → 항상 투명 -->
<Border BorderThickness="2" BorderBrush="Transparent">
    <Border.Style><Style TargetType="Border"><Style.Triggers>
        <DataTrigger Binding="{Binding IsActive}" Value="True">
            <Setter Property="BorderBrush" Value="#3B82F6"/>   <!-- 무시됨 -->

<!-- 올바름: 기본값을 스타일 기본 Setter로 → 트리거가 이를 덮어씀 -->
<Border BorderThickness="2">
    <Border.Style><Style TargetType="Border">
        <Setter Property="BorderBrush" Value="Transparent"/>   <!-- 기본값 -->
        <Style.Triggers>
            <DataTrigger Binding="{Binding IsActive}" Value="True">
                <Setter Property="BorderBrush" Value="#3B82F6"/>   <!-- 활성 시 적용 -->
```
- WPF는 한 속성 값이 여러 곳에서 지정되면 **우선순위**로 하나를 고른다: **로컬 값(요소에 직접 쓴 값) > 스타일 트리거 > 스타일 기본 Setter**.
- Border에 `BorderBrush="Transparent"`를 **직접** 쓰면 그게 "로컬 값"이라, `DataTrigger`(트리거)의 Setter가 이겨도 로컬 값을 **못 이겨** 항상 투명하게 남는다 → 파란 테두리가 절대 안 뜬다.
- **해결**: 기본값을 로컬 속성에서 떼어 **스타일의 기본 `<Setter>`**로 옮긴다. 같은 스타일 안에서는 트리거가 기본 Setter를 이기므로 `IsActive=true`에서 파란색이 적용된다.
- 교훈: "평소 값 + 조건부 값"을 함께 줄 땐 **둘 다 스타일 안**에 둔다(로컬 속성과 섞지 않는다).

---

## Step 6. Shell 조합

`Shell/ViewModels/MainWindowViewModel.cs` — 세션 관리자만 보유(레거시 3종 제거)

```csharp
using ControlTowerWin.Features.EmbeddedTerminal.ViewModels;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Shell.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public TerminalSessionsViewModel Sessions { get; }

    public MainWindowViewModel() => Sessions = new TerminalSessionsViewModel();
}
```

`Shell/Views/MainWindow.xaml` — 단일 ContentControl 조합

```xml
<Window x:Class="ControlTowerWin.Shell.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:etvm="clr-namespace:ControlTowerWin.Features.EmbeddedTerminal.ViewModels"
        xmlns:etv="clr-namespace:ControlTowerWin.Features.EmbeddedTerminal.Views"
        Title="Control Tower" Height="600" Width="900">
    <Window.Resources>
        <!-- VM-First 매핑: 세션 관리자 VM을 그릴 때 다중 세션 View 사용 -->
        <DataTemplate DataType="{x:Type etvm:TerminalSessionsViewModel}">
            <etv:TerminalSessionsView/>
        </DataTemplate>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="Control Tower"
                   HorizontalAlignment="Center" Margin="10" FontSize="24"/>

        <!-- 좌측 탭>터미널 트리 + 우측 임베드 터미널(다중, keep-alive) -->
        <ContentControl Grid.Row="1" Margin="10,0,10,10" Content="{Binding Sessions}"/>
    </Grid>
</Window>
```

> `SessionList`·`Terminal`(단일)·`NewTerminal` 바인딩과 그 `DataTemplate`을 모두 제거하고, `Sessions` 하나로 대체한다.
> `MainWindow.xaml.cs`는 06 그대로(`DataContext = new MainWindowViewModel()`).

### 🔤 C# 문법 짚기 (Step 6)

**① 식 본문 생성자 (expression-bodied constructor)**
```csharp
public MainWindowViewModel() => Sessions = new TerminalSessionsViewModel();
```
- 생성자 몸통이 한 줄(대입 하나)이라 `{ Sessions = ...; }` 대신 `=> ...`로 줄였다(Step 2 ④ `=>`와 같은 축약).
- 06에서는 `SessionList`·`NewTerminal`·`Terminal` 셋을 만들었지만, 이제 `Sessions` 하나만 조합한다 — Shell이 **얇아진다**(레거시 3종 제거 효과, Feature×Layer의 "Shell은 조합만" 원칙).

**② XAML 쪽 변화 요약**
- `<DataTemplate DataType="{x:Type etvm:TerminalSessionsViewModel}">`: "이 VM 타입을 그릴 땐 이 View를 써라"는 **VM→View 매핑**(VM-First 자동 연결).
- `<ContentControl Content="{Binding Sessions}"/>`: 그 자리에 `Sessions` VM을 꽂으면, 위 매핑에 따라 `TerminalSessionsView`가 자동으로 렌더된다.

---

## Step 7. 빌드 및 실행 검증

```powershell
cd src/ControlTowerWin
dotnet build ControlTowerWin.csproj
```

1. 빌드: **경고 0 / 오류 0** (레거시 참조가 남아 있으면 CS 오류 → Step 1·6 확인).
2. 실행: 탐색기에서 `bin/Debug/net10.0-windows/win-x64/ControlTowerWin.exe` 더블클릭.
   > 이전 인스턴스가 떠 있으면 exe가 잠겨 복사 실패(MSB3021) — 먼저 닫는다.
3. 검증:
   - 좌측 트리에 `tab1 > Terminal 1 (PID …)`, 우측에 pwsh 렌더.
   - **[새 터미널]** → tab1 아래 `Terminal 2`, 우측이 **2 pane 분할**.
   - 3~4개 → **2×2 타일** 동시 표시·각각 타이핑.
   - **[새 탭]** → `tab2` 전환, 우측이 tab2 것으로 교체.
   - tab1↔tab2 왕복 → **각 탭 터미널 모두 상태 유지**(keep-alive 핵심).
   - 트리에서 터미널 클릭 → 해당 pane **파란 테두리**.

---

## 체크리스트

- [ ] Step 1: `SessionMonitor`·`TerminalLauncher` 폴더 삭제(레거시)
- [ ] Step 2: `TerminalViewModel`에 `IsActive`·`Pid`·`TreeLabel`
- [ ] Step 3: `TabViewModel`(Terminals·IsSelected·SelectedTerminal·Add/RemoveTerminal)
- [ ] Step 4: `TerminalSessionsViewModel`(Tabs·SelectedTab·Add/Add터미널/Close 커맨드)
- [ ] Step 5: `TerminalSessionsView` 좌 TreeView + 우 바깥 ItemsControl(Grid,Visibility)/안쪽 ItemsControl(UniformGrid), 코드비하인드 `SelectedItemChanged`
- [ ] Step 6: `MainWindowViewModel`=`Sessions`만, `MainWindow`=단일 ContentControl
- [ ] Step 7: 빌드 0/0, 탭 전환 시 세션 유지(keep-alive), pane 타일·포커스 테두리

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| **탭 전환 시 터미널이 검정/재시작** | `TabControl` 사용 → 콘텐츠 재생성으로 ConPTY 파괴 | `ItemsControl`+Grid 겹침+`Visibility` keep-alive(개념 §3) |
| 트리 노드에 터미널이 통째로 렌더됨 | `TerminalViewModel` 암시적 DataTemplate이 트리에도 적용 | 트리용=`HierarchicalDataTemplate.ItemTemplate`, 우측용=`x:Key` 키드 템플릿으로 분리(§4) |
| 트리 선택이 VM에 반영 안 됨 | `TreeView.SelectedItem`은 읽기전용(바인딩 불가) | 코드비하인드 `SelectedItemChanged`로 반영(Step 5) |
| pane 위에 WPF가 안 겹쳐짐 | airspace(native HwndHost) | 정상. 타일은 안 겹치므로 무관, 테두리는 native 밖이라 OK |
| 빌드 CS 오류(SessionMonitor/TerminalLauncher 없음) | Shell이 삭제된 레거시를 참조 | Step 6의 `MainWindowViewModel`/`MainWindow` 교체 |
| 탭이 접혀 터미널이 안 보임 | TreeViewItem 기본 미확장 | `ItemContainerStyle`에 `IsExpanded=True` |
| **포커스 pane 파란 테두리가 안 뜸** | `BorderBrush="Transparent"`를 요소에 직접 지정 → 로컬 값이 DataTrigger를 이김(WPF 우선순위) | 기본값을 요소에서 떼어 스타일 기본 `<Setter Property="BorderBrush" Value="Transparent"/>`로 이동(문법 박스 Step 5 ⑥) |

---

## 미결 / 후속

- **PID 실값 연결**: 지금은 `(PID …)` 자리표시자. `EasyTerminalControl`의 프로세스/ConPTY 핸들에서
  자식(pwsh) PID를 얻어 `TerminalViewModel.Pid`에 세팅하면 트리에 바로 반영된다(구조는 준비됨).
- **수동 pane 분할 핸들/크기 조정(FR-044)**: 현재는 `UniformGrid` 자동 타일. `GridSplitter` 기반 수동 분할은 후속.
- **세션 전환 단축키(FR-045, Ctrl+1…N)**: 좌측 트리/포커스와 연동하는 키 바인딩 후속.
- **탭/터미널 닫힘 시 좀비 정리**: View 제거 시 ConPTY 자식 종료 보장은 Runbook 09(수명 관리) 소관.

---

## 다음 단계

- **08**: 앱→터미널 **명령 주입** + **출력 가로채기/로깅**(`TermPTY.WriteToTerm`/`InterceptOutputToUITerminal`).
- **09**: 임베드 세션 **종료/수명** 관리(`RestartTerm`/`DisconnectConPTYTerm`) + 좀비 정리.

---

## 참고

- [Runbook 06 — 임베드 터미널 (PoC + 최소 임베드)](./06_wpf_embedded_terminal.md) — `TerminalView`/native 배포
- [Runbook 04 — 디렉토리 구조화](./04_wpf_directory_structuring.md) · [feature-module.md](../guides/convention/feature-module.md) · [viewmodels.md](../guides/convention/viewmodels.md) · [views.md](../guides/convention/views.md)
- 기획: [`04_requirements`](../plans/v1/04_requirements.md)(FR-006·008·044·045·039) · [`07_interfaces`](../plans/v1/07_interfaces.md)(SC-07·10) · [`12_roadmap`](../plans/v1/12_roadmap.md)(L0)
- [EasyWindowsTerminalControl (GitHub)](https://github.com/mitchcapper/EasyWindowsTerminalControl)
