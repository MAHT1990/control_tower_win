# Runbook 12 — pane 수동 크기조정: 리사이즈 타일 패널 (FR-044 / SC-10)

> **방식**: 직접 구현·검증 후 역산출. 코드는 `.NET 10 / net10.0-windows`에서 빌드 0/0·실행 검증 완료(커밋 예정).
> **🔤 C# 문법 짚기**: 각 Step 끝에 그 단계에서 새로 등장한 문법 박스(06~11에서 다룬 건 반복하지 않음).

---

## 목표

Runbook 07의 **UniformGrid 자동 타일**에 **수동 크기조정**을 더한다.

- pane 사이 **경계(gutter)를 마우스로 드래그**해 인접 pane 크기를 재분배한다.
- **최소 크기 가드**(40px 이하로 안 줄어듦)로 pane 소실을 막는다.
- 조정한 열·행 비율은 **런타임 레이아웃**으로 유지된다(탭 전환에도 보존).

> 기획 근거: FR-044(pane 위치·크기 조정) · FN-TRM-14 · SC-10(pane 분할 레이아웃) · ENT-003.pane_layout(런타임·비영속).
> 이 런북은 RB07 "미결/후속"의 *"수동 pane 분할 핸들/크기 조정(FR-044): GridSplitter 기반 수동 분할은 후속"* 항목을 해소한다.

---

## 전제

- [ ] Runbook 07 완료 — 탭>터미널 트리 + keep-alive `UniformGrid` pane 분할(`TerminalSessionsView`).
- [ ] `Shared/Core`의 기반(04). (본 런북은 `Shared/Controls/`를 처음 만든다.)

---

## 개념 (큰 틀)

### 1. 왜 `Grid` + `GridSplitter`로 갈아엎지 않나 (이 런북의 핵심 함정)

가장 흔한 "리사이즈 가능한 분할"은 `Grid` + `GridSplitter`다. 하지만 우리 pane은 **터미널 개수가 동적**이라, 개수가 바뀔 때마다 `Grid`를 재생성하며 터미널을 **재부모화**해야 한다. 그런데 터미널은 native `HwndHost`라 트리에서 제거되는 순간 `Unloaded`가 발화하고, RB09에서 건 정리 훅이 `DisconnectConPTYTerm`을 불러 **ConPTY 세션이 죽는다**(RB07의 TabControl 함정과 같은 뿌리).

```
Grid 재생성 → 터미널 View 재부모화 → Unloaded 발화 → Cleanup() → 세션 사망 ✗
```

그래서 **RB07의 `ItemsControl`(아이템 컨테이너를 파괴하지 않음) 구조를 그대로 두고**, 그 `ItemsPanel`만 **커스텀 리사이즈 패널**로 바꾼다. 컨테이너가 안정적으로 살아 있어 세션이 유지된다(keep-alive).

### 2. gutter = native pane 사이 WPF 공백 (airspace 안전)

타일 셀 사이에 6px 틈(gutter)을 두고, 그 틈을 분할 핸들로 쓴다. 틈에는 native 터미널이 없으므로 **WPF가 마우스를 받을 수 있고**(airspace 무관), 드래그 중 커서가 native pane 위로 넘어가도 `Mouse.Capture`로 이벤트를 계속 받는다(Win32 `SetCapture`가 캡처한 창으로 모든 마우스 메시지를 몰아줌).

```
┌──────────┐ gutter ┌──────────┐
│ PANE A   │◀─6px──▶│ PANE B   │   틈에 마우스 → ↔ 커서 → 드래그로 A/B 재분배
│ (HwndHost)│  ↕↔   │ (HwndHost)│   (드래그는 Mouse.Capture로 native 위도 수신)
└──────────┘        └──────────┘
```

### 3. 타일 산출 + 비율 배열 = 런타임 레이아웃(ENT-003.pane_layout)

RB07 UniformGrid와 동일하게 `cols=⌈√n⌉ · rows=⌈n/cols⌉`로 격자를 잡되, **열 비율 `_colFrac[]` · 행 비율 `_rowFrac[]`**(합=1)를 패널이 들고 있다가 드래그로 조정한다. 이 비율이 곧 ENT-003.pane_layout(런타임·비영속)이다. 탭은 keep-alive라 탭별 패널 인스턴스가 살아 있어 **탭 전환에도 레이아웃이 유지**된다. (앱 재시작 후 복원은 ENT-017/FR-043 소관 → 본 런북 범위 밖.)

---

## 아키텍처 통합 (Feature × Layer)

```
Shared/
└── Controls/
    └── ResizableTilePanel.cs      # (신규) 재사용 커스텀 Panel — 타일 + gutter 드래그 리사이즈

Features/EmbeddedTerminal/Views/
└── TerminalSessionsView.xaml      # (수정) 내부 ItemsPanel: UniformGrid → ResizableTilePanel
```

> 타일 패널은 터미널에 국한되지 않는 **범용 레이아웃 컨트롤**이라 `Shared/Controls/`에 둔다(shared.md 컨벤션). Shared는 최하위 레이어라 WPF에만 의존한다.

---

## Step 1. ResizableTilePanel — 커스텀 분할 패널

`Shared/Controls/ResizableTilePanel.cs` (신규)

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ControlTowerWin.Shared.Controls;

/// <summary>
/// 자식들을 자동 타일 그리드(UniformGrid식: cols=⌈√n⌉, rows=⌈n/cols⌉)로 배치하되,
/// 셀 사이 gutter(분할 핸들)를 드래그해 인접 열·행 크기를 재분배하는 분할 패널
/// (FR-044 / FN-TRM-14 / SC-10). 열·행 비율은 런타임 상태(ENT-003.pane_layout, 비영속)로
/// 패널 인스턴스에 보존된다 — 탭 keep-alive 덕에 탭 전환에도 유지된다.
/// keep-alive: 자식(터미널 View, HwndHost)을 제거·재부모화하지 않아 ConPTY 세션이 유지된다.
/// gutter는 native pane 사이 WPF 공백이라 airspace 안전, 드래그는 Mouse.Capture로 수신한다.
/// </summary>
public class ResizableTilePanel : Panel
{
    private const double Gutter = 6.0;   /* 분할 핸들(gutter) 두께 */
    private const double MinCell = 40.0; /* 최소 크기 가드(AC②) */

    private double[] _colFrac = Array.Empty<double>();
    private double[] _rowFrac = Array.Empty<double>();
    private int _cols;
    private int _rows;

    /* arrange 결과(마우스 히트 테스트용) */
    private double[] _colOffsets = Array.Empty<double>();
    private double[] _colWidths = Array.Empty<double>();
    private double[] _rowOffsets = Array.Empty<double>();
    private double[] _rowHeights = Array.Empty<double>();

    /* 드래그 상태 */
    private bool _dragging;
    private bool _dragVertical;
    private int _dragIndex;   /* 경계 인덱스(우측/아래 셀) */
    private Point _dragStart;
    private double _origA;     /* 드래그 시작 시 인접 두 셀 비율 */
    private double _origB;

    public ResizableTilePanel()
    {
        /* gutter(자식 없는 공백) 위 마우스를 수신하려면 투명 배경 필요 */
        Background = Brushes.Transparent;
    }

    /* 자식 수 n에서 타일 격자(cols·rows) 산출 + 비율 배열 크기 정합(변하면 균등 리셋) */
    private void EnsureGrid(int n)
    {
        int cols = n <= 1 ? 1 : (int)Math.Ceiling(Math.Sqrt(n));
        int rows = n == 0 ? 1 : (int)Math.Ceiling((double)n / cols);
        if (cols != _cols || _colFrac.Length != cols)
        {
            _cols = cols;
            _colFrac = Uniform(cols);
        }
        if (rows != _rows || _rowFrac.Length != rows)
        {
            _rows = rows;
            _rowFrac = Uniform(rows);
        }
    }

    private static double[] Uniform(int k)
    {
        var a = new double[k];
        for (int i = 0; i < k; i++) a[i] = 1.0 / k;
        return a;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int n = InternalChildren.Count;
        EnsureGrid(n);

        double w = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
        double cellW = Math.Max(0, w - Gutter * (_cols - 1));
        double cellH = Math.Max(0, h - Gutter * (_rows - 1));

        for (int i = 0; i < n; i++)
        {
            int col = i % _cols;
            int row = i / _cols;
            InternalChildren[i].Measure(new Size(cellW * _colFrac[col], cellH * _rowFrac[row]));
        }
        return new Size(w, h);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int n = InternalChildren.Count;
        EnsureGrid(n);

        double cellW = Math.Max(0, finalSize.Width - Gutter * (_cols - 1));
        double cellH = Math.Max(0, finalSize.Height - Gutter * (_rows - 1));

        _colWidths = new double[_cols];
        _colOffsets = new double[_cols];
        double x = 0;
        for (int c = 0; c < _cols; c++)
        {
            _colWidths[c] = cellW * _colFrac[c];
            _colOffsets[c] = x;
            x += _colWidths[c] + Gutter;
        }

        _rowHeights = new double[_rows];
        _rowOffsets = new double[_rows];
        double y = 0;
        for (int r = 0; r < _rows; r++)
        {
            _rowHeights[r] = cellH * _rowFrac[r];
            _rowOffsets[r] = y;
            y += _rowHeights[r] + Gutter;
        }

        for (int i = 0; i < n; i++)
        {
            int col = i % _cols;
            int row = i / _cols;
            InternalChildren[i].Arrange(new Rect(_colOffsets[col], _rowOffsets[row], _colWidths[col], _rowHeights[row]));
        }
        return finalSize;
    }

    /* 반환: 0=경계 없음, 1=수직 경계(열 사이), 2=수평 경계(행 사이). idx=경계 우/아래 셀 인덱스. */
    private int HitBoundary(Point p, out int idx)
    {
        idx = -1;
        for (int c = 1; c < _cols && c < _colOffsets.Length; c++)
        {
            if (p.X >= _colOffsets[c] - Gutter && p.X <= _colOffsets[c])
            {
                idx = c;
                return 1;
            }
        }
        for (int r = 1; r < _rows && r < _rowOffsets.Length; r++)
        {
            if (p.Y >= _rowOffsets[r] - Gutter && p.Y <= _rowOffsets[r])
            {
                idx = r;
                return 2;
            }
        }
        return 0;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this);
        if (_dragging)
        {
            if (_dragVertical) DragColumn(p);
            else DragRow(p);
            return;
        }
        Cursor = HitBoundary(p, out _) switch
        {
            1 => Cursors.SizeWE,
            2 => Cursors.SizeNS,
            _ => Cursors.Arrow,
        };
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var p = e.GetPosition(this);
        int hit = HitBoundary(p, out int idx);
        if (hit == 0) return;

        _dragging = true;
        _dragVertical = hit == 1;
        _dragIndex = idx;
        _dragStart = p;
        if (_dragVertical)
        {
            _origA = _colFrac[idx - 1];
            _origB = _colFrac[idx];
        }
        else
        {
            _origA = _rowFrac[idx - 1];
            _origB = _rowFrac[idx];
        }
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    /* 수직 경계 드래그 → 인접 두 열 비율 재분배(최소 크기 가드) */
    private void DragColumn(Point p)
    {
        double cellW = ActualWidth - Gutter * (_cols - 1);
        if (cellW <= 0) return;
        double minFrac = MinCell / cellW;
        double sum = _origA + _origB;
        if (sum < 2 * minFrac) return;

        double df = (p.X - _dragStart.X) / cellW;
        double a = _origA + df;
        double b = sum - a;
        if (a < minFrac) { a = minFrac; b = sum - a; }
        else if (b < minFrac) { b = minFrac; a = sum - b; }

        _colFrac[_dragIndex - 1] = a;
        _colFrac[_dragIndex] = b;
        InvalidateArrange();
    }

    /* 수평 경계 드래그 → 인접 두 행 비율 재분배(최소 크기 가드) */
    private void DragRow(Point p)
    {
        double cellH = ActualHeight - Gutter * (_rows - 1);
        if (cellH <= 0) return;
        double minFrac = MinCell / cellH;
        double sum = _origA + _origB;
        if (sum < 2 * minFrac) return;

        double df = (p.Y - _dragStart.Y) / cellH;
        double a = _origA + df;
        double b = sum - a;
        if (a < minFrac) { a = minFrac; b = sum - a; }
        else if (b < minFrac) { b = minFrac; a = sum - b; }

        _rowFrac[_dragIndex - 1] = a;
        _rowFrac[_dragIndex] = b;
        InvalidateArrange();
    }
}
```

### 왜 이렇게 짜는가 (핵심)

- **`EnsureGrid`**: 자식 수 n에서 `cols=⌈√n⌉`(RB07 UniformGrid와 동일 배치). 개수가 바뀌면 비율 배열을 균등으로 리셋(2→3처럼 격자가 바뀔 때). 개수가 같은 열·행 수를 유지하면(예 3→4, 둘 다 2×2) 조정한 비율을 보존.
- **Measure/Arrange**: 전체 폭에서 gutter 총합을 뺀 "셀 총면적"에 비율을 곱해 각 셀 크기를 산출, `i → (row=i/cols, col=i%cols)` 위치에 배치. gutter만큼 셀 사이를 벌린다.
- **HitBoundary**: 마우스가 어느 gutter(열 사이/행 사이) 위인지 판정. gutter 범위 `[colOffsets[c]-Gutter, colOffsets[c]]` = 앞 열의 오른쪽 끝 ~ 뒤 열의 왼쪽 끝.
- **드래그**: 픽셀 이동량을 셀 총면적 대비 비율(df)로 바꿔 인접 두 셀 비율을 한쪽에서 떼 다른 쪽에 준다(합 보존). `minFrac` 이하로는 못 줄이게 클램프(AC② 최소 크기 가드).

### 🔤 C# 문법 짚기 (Step 1)

**① 커스텀 `Panel` 상속 + `MeasureOverride`/`ArrangeOverride` (WPF 2-pass 레이아웃)**
```csharp
public class ResizableTilePanel : Panel { ... }
protected override Size MeasureOverride(Size availableSize) { ... }
protected override Size ArrangeOverride(Size finalSize) { ... }
```
- WPF 레이아웃은 **2단계**다: **Measure**("주어진 공간에서 얼마나 필요한가"를 자식마다 `child.Measure(...)`로 물어봄) → **Arrange**("실제로 어디에 얼마 크기로 놓을지"를 `child.Arrange(rect)`로 확정). 커스텀 배치가 필요하면 이 두 메서드를 재정의해 **내가 직접 자식 위치·크기를 계산**한다. `UniformGrid`도 내부적으로 이 두 개를 구현한 Panel일 뿐 — 우리는 거기에 비율·gutter 개념을 더한 것.

**② `InternalChildren`**
```csharp
int n = InternalChildren.Count;
InternalChildren[i].Measure(...);
```
- Panel이 그리는 **자식 UI 요소 컬렉션**. `ItemsControl`의 `ItemsPanel`로 쓰이면, 각 자식 = 아이템 컨테이너(터미널 pane). 우리는 이걸 **읽어서 배치만** 하고 **추가/제거하지 않는다** → 컨테이너가 안정 유지 → keep-alive(세션 생존).

**③ 라우팅 이벤트 재정의 `protected override void OnMouse*` + `Mouse.Capture`**
```csharp
protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { ...; CaptureMouse(); e.Handled = true; }
protected override void OnMouseMove(MouseEventArgs e) { ... }
protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { ...; ReleaseMouseCapture(); }
```
- WPF 요소는 마우스 이벤트를 `On***`로 재정의해 처리할 수 있다(핸들러 등록 대신 상속으로). `CaptureMouse()`는 "이제부터 마우스는 이 요소가 독점"(Win32 `SetCapture`). 드래그 중 커서가 **native 터미널 위로 넘어가도** 이벤트가 이 패널로 계속 온다 — airspace 위 드래그의 핵심. `e.Handled = true`로 이벤트를 여기서 소비(트리 상위로 안 흘림).

**④ `switch` 식(expression)**
```csharp
Cursor = HitBoundary(p, out _) switch { 1 => Cursors.SizeWE, 2 => Cursors.SizeNS, _ => Cursors.Arrow, };
```
- RB05의 `switch` **문(statement)** 과 달리 `switch` **식**은 값을 **바로 돌려준다**. "경계 종류(1/2/0)에 따라 커서를 골라 대입"을 한 줄로. `out _`는 idx 반환값을 안 쓴다는 버림(discard).

**⑤ `out` 매개변수 + `Array.Empty<T>()`**
```csharp
private int HitBoundary(Point p, out int idx) { idx = -1; ... }
private double[] _colFrac = Array.Empty<double>();
```
- `out`은 메서드가 **결과를 여러 개 내보내는** 통로(반환값=경계 종류, `idx`=경계 인덱스). 호출부는 `HitBoundary(p, out int idx)`로 받는다. `Array.Empty<double>()`는 길이 0 배열의 **재사용 싱글턴** — null 대신 빈 배열로 초기화해 `.Length` 접근을 안전하게.

---

## Step 2. TerminalSessionsView — 내부 ItemsPanel 교체

`Features/EmbeddedTerminal/Views/TerminalSessionsView.xaml`

① UserControl 루트에 네임스페이스 추가:

```xml
<UserControl ...
             xmlns:local="clr-namespace:ControlTowerWin.Features.EmbeddedTerminal.Views"
             xmlns:ctrl="clr-namespace:ControlTowerWin.Shared.Controls">
```

② 우측 pane의 **내부** `ItemsControl`(선택 탭의 터미널을 타일하는 것)의 `ItemsPanel`만 교체:

```xml
<ItemsControl ItemsSource="{Binding Terminals}"
              ItemTemplate="{StaticResource TerminalPane}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <ctrl:ResizableTilePanel/>   <!-- 07: UniformGrid → 12: ResizableTilePanel -->
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
</ItemsControl>
```

> **바깥** `ItemsControl`(탭 keep-alive: 패널=Grid + `Visibility`)과 `TerminalPane` 템플릿(테두리 + `TerminalView`)은 **07 그대로 둔다**. 딱 이 한 줄(`UniformGrid` → `ResizableTilePanel`)만 바꾼다.

---

## Step 3. 빌드 및 실행 검증

```powershell
cd src/ControlTowerWin
dotnet build ControlTowerWin.csproj
```

1. 빌드 **경고 0 / 오류 0**.
2. 실행: 탐색기에서 `bin/Debug/net10.0-windows/win-x64/ControlTowerWin.exe` 더블클릭.
   > Git Bash 백그라운드 실행은 콘솔 컨텍스트 차이로 세션이 조기 종료될 수 있다(RB06) → **더블클릭/F5로 검증**.
3. 검증:
   - 한 탭에서 **[새 터미널]**로 pane 2~4개 → 기존과 같은 타일 배치(1→1, 2→좌우, 3~4→2×2).
   - pane **사이 6px 틈에 마우스** → 커서가 **↔(수직 경계)/↕(수평 경계)** 로 바뀜.
   - **드래그** → 인접 pane 크기 재분배. 아주 작게 끌어도 **40px 이하로 안 줄어듦**(min 가드).
   - 드래그 중 커서가 터미널 위로 넘어가도 **끊김 없이** 조정(Mouse.Capture).
   - **탭 전환 후 복귀** → 조정한 레이아웃 유지(keep-alive). 세션도 그대로 살아 있음.

---

## 체크리스트

- [ ] Step 1: `Shared/Controls/ResizableTilePanel.cs` — `Panel` 상속, `Measure/ArrangeOverride`, gutter 히트 테스트, 드래그 재분배(min 가드), `Background=Transparent`, `CaptureMouse`
- [ ] Step 2: `TerminalSessionsView.xaml` — `xmlns:ctrl` 추가 + 내부 `ItemsPanel` `UniformGrid`→`ResizableTilePanel`(1줄)
- [ ] Step 3: 빌드 0/0 + 드래그 크기조정·min 가드·탭 전환 유지·세션 생존 검증

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| gutter 위에서 커서 안 바뀜/드래그 시작 안 됨 | Panel `Background`가 null → 빈 공백이 마우스를 안 받음 | 생성자에서 `Background = Brushes.Transparent` |
| 드래그가 터미널 위로 가면 끊김 | 마우스 캡처 누락 | 시작 시 `CaptureMouse()`, 종료 시 `ReleaseMouseCapture()` |
| 탭 전환 후 세션이 죽음/재시작 | 패널이 자식을 제거·재부모화 | `InternalChildren`는 **읽기만**(추가/제거 금지). keep-alive는 07 구조 유지 |
| pane가 0크기로 사라짐 | 최소 크기 가드 없음 | `minFrac = MinCell/cellSize`로 클램프(`DragColumn`/`DragRow`) |
| 터미널 추가/삭제하면 조정한 크기 리셋 | 격자(cols·rows) 변경 시 비율 균등 리셋 | 의도된 동작(격자가 바뀌면 균등부터 다시). 같은 격자 유지 시엔 비율 보존 |
| 앱 재시작 후 레이아웃 안 남음 | pane_layout 영속 미구현 | 정상 — 영속·복원은 ENT-017/FR-043(M-RESTORE) 소관, 본 런북 범위 밖 |

---

## 다음 단계 / 참고

- **FR-045**(세션 전환 단축키 Ctrl+1…N)·**FR-043**(레이아웃 영속·복원, M-RESTORE) 등 L0/후속.
- [Runbook 07](./07_wpf_multi_terminal_tabs.md)(UniformGrid keep-alive·airspace) · [09](./09_wpf_session_termination.md)(Unloaded→Cleanup 세션 정리)
- 기획: [`04_requirements`](../plans/v1/04_requirements.md)(FR-044) · [`07_interfaces`](../plans/v1/07_interfaces.md)(SC-10) · [`09_database`](../plans/v1/09_database.md)(ENT-003.pane_layout) · [`05_functions`](../plans/v1/05_functions.md)(FN-TRM-14)
