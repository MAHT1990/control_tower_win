# Runbook 10 — 세션 액션 컨텍스트 메뉴(SC-23) + 트리 인라인 이름 변경(FR-046)

> **방식**: 직접 구현·검증 후 역산출. 코드는 빌드 0/0·실행 검증 완료(커밋 `d316173`).
> **🔤 C# 문법 짚기**: 각 Step 끝에 새 문법 박스(07·08에서 다룬 건 반복하지 않음).

---

## 목표

좌측 트리/pane에 **세션 액션 컨텍스트 메뉴**와 **인라인 이름 변경**을 더한다.

1. **컨텍스트 메뉴(SC-23)**: 트리 노드/pane 우클릭 → `명령 실행 · 이름 변경 · 재시작 · 종료`.
2. **인라인 rename(FR-046)**: 트리 노드 더블클릭/F2 → 그 자리에서 이름 편집(탭·터미널의 **표시 이름**, `as`와 분리·런타임).

> 기획 근거: FR-046(표시 이름 변경)·FR-014(명령 실행)·FR-015(종료)·FR-018(재시작) / SC-23·FN-TRM-16.
> "명령 실행"→08의 커맨드 바 포커스, "재시작"→09의 RestartCommand, "종료"→07의 CloseCommand.

---

## 전제

- [ ] Runbook 07(트리·pane)·08(커맨드 바·InjectCommand)·09(RestartCommand) 완료

---

## 개념 (큰 틀)

```
트리/pane 우클릭 ─▶ 대상 노드 먼저 선택 ─▶ ContextMenu
                     (코드비하인드)          ├─ 명령 실행 → 커맨드 바 포커스(08)
                                             ├─ 이름 변경 → IsEditing=true (인라인 편집)
                                             ├─ 재시작    → RestartCommand(09)
                                             └─ 종료      → CloseCommand(07)

더블클릭/F2 ─▶ IsEditing=true ─▶ 라벨 숨김/TextBox 노출 ─▶ Enter=커밋 / Esc=취소
```

### `IRenamableNode` — 탭·터미널 공통 rename 계약

탭이든 터미널이든 "이름표를 바꿀 수 있는 물건". 코드비하인드가 노드 종류 무관하게 편집을 다루게 한다.

---

## Step 1. IRenamableNode 계약

`Interfaces/IRenamableNode.cs` (신규)

```csharp
namespace ControlTowerWin.Features.EmbeddedTerminal.Interfaces;

public interface IRenamableNode
{
    /* 표시 이름(디스플레이 라벨). 빈 문자열 세팅은 구현체가 거부(이전 이름 유지). */
    string Title { get; set; }

    /* 인라인 편집 모드 토글(트리 노드의 TextBox 노출) */
    bool IsEditing { get; set; }
}
```

---

## Step 2. TerminalViewModel / TabViewModel — Title 편집화 + IsEditing

두 VM 모두 `IRenamableNode`를 구현한다. 읽기전용이던 `Title`을 **set 가능**으로 바꾸되 **빈 문자열 거부**, `IsEditing` 추가.

```csharp
public class TerminalViewModel : ViewModelBase, IRenamableNode   // (TabViewModel도 동일 패턴)
{
    private bool _isEditing;
    private string _title;

    public TerminalViewModel(string title) => _title = title;

    /* 표시 이름. 빈 문자열은 거부(이전 이름 유지). */
    public string Title
    {
        get => _title;
        set
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return;
            _title = trimmed;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TreeLabel));   // 터미널만 (TreeLabel = 이름+PID)
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(); }
    }
    /* ... 나머지 기존 멤버 ... */
}
```

### 🔤 C# 문법 짚기 (Step 2)

**인터페이스 구현 + 빈값 거부 setter**
- `: ViewModelBase, IRenamableNode` — 부모 클래스 하나 + 인터페이스(계약) 하나를 동시에. 인터페이스가 요구한 `Title`(set)·`IsEditing`을 이 클래스가 실제로 채운다.
- setter의 `if (string.IsNullOrEmpty(trimmed)) return;` — 빈 이름이 들어오면 **아무것도 안 하고 되돌아감** → 이전 이름 유지(거부).

---

## Step 3. TerminalSessionsViewModel — 선택 노드 + 컨텍스트 커맨드

```csharp
    private IRenamableNode? _selectedNode;

    public ICommand RenameCommand { get; }
    public ICommand RunCommand { get; }        // "명령 실행" → 커맨드 바 포커스

    /* 생성자:
    RenameCommand = new RelayCommand(_ => BeginRename(), _ => SelectedNode != null);
    RunCommand    = new RelayCommand(_ => FocusCommandBarRequested?.Invoke(),
                                     _ => SelectedTab?.SelectedTerminal != null);
    (RestartCommand=09, CloseCommand=07 재사용)
    */

    /* 좌측 트리에서 현재 선택된 노드(탭 또는 터미널) — 이름 변경 대상 */
    public IRenamableNode? SelectedNode
    {
        get => _selectedNode;
        set { _selectedNode = value; OnPropertyChanged(); }
    }

    private void BeginRename()
    {
        if (SelectedNode != null) SelectedNode.IsEditing = true;
    }
```

> `FocusCommandBarRequested`는 08에서 선언한 이벤트 — "명령 실행"이 커맨드 바에 포커스를 요청한다.

---

## Step 4. TerminalSessionsView.xaml — 컨텍스트 메뉴 + 인라인 편집 템플릿

**① rename용 스타일** (UserControl.Resources) — 기본값을 **로컬 속성이 아니라 스타일 Setter**로(07 ⑥ 우선순위 함정 회피)

```xml
<Style x:Key="RenameLabel" TargetType="TextBlock">
    <Style.Triggers>
        <DataTrigger Binding="{Binding IsEditing}" Value="True">
            <Setter Property="Visibility" Value="Collapsed"/>
        </DataTrigger>
    </Style.Triggers>
</Style>
<Style x:Key="RenameBox" TargetType="TextBox">
    <Setter Property="Visibility" Value="Collapsed"/>
    <Style.Triggers>
        <DataTrigger Binding="{Binding IsEditing}" Value="True">
            <Setter Property="Visibility" Value="Visible"/>
        </DataTrigger>
    </Style.Triggers>
</Style>
```

**② 트리 노드 = 라벨(TextBlock) + 편집(TextBox) 토글**

```xml
<Grid>
    <TextBlock Text="{Binding Title}" Style="{StaticResource RenameLabel}"
               MouseLeftButtonDown="Label_MouseDown"/>
    <TextBox Text="{Binding Title, Mode=TwoWay, UpdateSourceTrigger=Explicit}"
             Style="{StaticResource RenameBox}"
             IsVisibleChanged="RenameBox_VisibleChanged"
             KeyDown="RenameBox_KeyDown" LostFocus="RenameBox_LostFocus"/>
</Grid>
```

**③ TreeView 컨텍스트 메뉴(SC-23)** — `PlacementTarget.DataContext`로 VM 커맨드 연결

```xml
<TreeView x:Name="SessionTree" ItemsSource="{Binding Tabs}"
          SelectedItemChanged="OnTreeSelectionChanged"
          PreviewMouseRightButtonDown="OnTreeRightClick"
          PreviewKeyDown="OnTreeKeyDown">
    <TreeView.ContextMenu>
        <ContextMenu>
            <MenuItem Header="명령 실행" Command="{Binding PlacementTarget.DataContext.RunCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}"/>
            <MenuItem Header="이름 변경" Command="{Binding PlacementTarget.DataContext.RenameCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}"/>
            <Separator/>
            <MenuItem Header="재시작" Command="{Binding PlacementTarget.DataContext.RestartCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}"/>
            <MenuItem Header="종료" Command="{Binding PlacementTarget.DataContext.CloseCommand, RelativeSource={RelativeSource AncestorType=ContextMenu}}"/>
        </ContextMenu>
    </TreeView.ContextMenu>
    <!-- ItemContainerStyle: IsExpanded=True (07) -->
</TreeView>
```

### 🔤 C# 문법 짚기 (Step 4)

**`RelativeSource AncestorType=ContextMenu` + `PlacementTarget`**
- ContextMenu는 **별도 비주얼 트리**라 TreeView의 DataContext(VM)를 자동 상속받지 못한다. `AncestorType=ContextMenu`로 자신(ContextMenu)을 찾고, `PlacementTarget`(= 메뉴가 붙은 TreeView)의 `DataContext`(= VM)에서 커맨드를 가져온다. "메뉴 → 붙은 컨트롤 → 그 VM"으로 거슬러 올라가는 경로.

---

## Step 5. TerminalSessionsView.xaml.cs — 우클릭 선택·F2·더블클릭·커밋/취소

```csharp
/* 선택 변경 시 SelectedNode 반영(rename 대상) — 기존 OnTreeSelectionChanged에 추가 */
vm.SelectedNode = e.NewValue as IRenamableNode;

/* 우클릭 시 대상 노드를 먼저 선택 → 컨텍스트 메뉴가 그 노드에 작용 */
private void OnTreeRightClick(object sender, MouseButtonEventArgs e)
{
    var item = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
    if (item != null) item.IsSelected = true;
}

/* F2 → 선택 노드 편집 시작 */
private void OnTreeKeyDown(object sender, KeyEventArgs e)
{
    if (e.Key != Key.F2) return;
    if (DataContext is TerminalSessionsViewModel vm && vm.SelectedNode is IRenamableNode node)
    { node.IsEditing = true; e.Handled = true; }
}

/* 라벨 더블클릭 → 편집 시작 */
private void Label_MouseDown(object sender, MouseButtonEventArgs e)
{
    if (e.ClickCount == 2 && sender is FrameworkElement fe && fe.DataContext is IRenamableNode node)
    { node.IsEditing = true; e.Handled = true; }
}

/* TextBox가 보이게 되는 순간 포커스+전체선택 (Loaded는 최초 1회만이라 IsVisibleChanged 사용) */
private void RenameBox_VisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
{
    if (sender is TextBox tb && tb.IsVisible)
        tb.Dispatcher.BeginInvoke(new Action(() => { tb.Focus(); tb.SelectAll(); }), DispatcherPriority.Input);
}

/* Enter=커밋, Esc=취소 */
private void RenameBox_KeyDown(object sender, KeyEventArgs e)
{
    if (sender is not TextBox tb) return;
    if (e.Key == Key.Enter) { CommitRename(tb); e.Handled = true; }
    else if (e.Key == Key.Escape) { CancelRename(tb); e.Handled = true; }
}
private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
{ if (sender is TextBox tb) CommitRename(tb); }

/* 편집값을 Title에 반영(빈값은 setter가 거부) 후 편집 종료 */
private static void CommitRename(TextBox tb)
{
    tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    if (tb.DataContext is IRenamableNode node) node.IsEditing = false;
}
/* 원복 후 편집 종료 (이후 LostFocus 커밋을 무해화) */
private static void CancelRename(TextBox tb)
{
    if (tb.DataContext is IRenamableNode node) { tb.Text = node.Title; node.IsEditing = false; }
}
```

### 🔤 C# 문법 짚기 (Step 5)

**① `UpdateSourceTrigger=Explicit` + `GetBindingExpression().UpdateSource()`**
- 보통 TextBox는 타이핑마다/포커스 이탈 시 VM에 값을 밀어 넣는다. 여기선 `Explicit`으로 두어 **내가 시킬 때만** 밀어 넣는다. Enter/포커스이탈에서 `UpdateSource()`를 호출해 커밋하고, Esc에선 호출 안 함(취소). 커밋/취소를 정확히 통제하는 방법.

**② `IsVisibleChanged` (Loaded 대신)**
- TextBox는 트리에 한 번만 생성되고 평소 숨김이라 `Loaded`는 최초 1회만 발화한다. 재편집마다 포커스를 주려면 **보임/숨김이 바뀔 때**(`IsVisibleChanged`) 잡아야 한다. `Dispatcher.BeginInvoke(..., Input)`으로 레이아웃이 끝난 뒤 포커스.

**③ Esc 취소의 함정**
- Esc → `IsEditing=false` → TextBox 숨김 → `LostFocus` 발화 → 커밋될 위험. 그래서 취소 시 **먼저 `tb.Text = node.Title`로 원복**해 두면, 뒤이은 LostFocus 커밋이 원래 값을 다시 써 무해해진다.

---

## Step 6. 빌드·검증 / 트러블슈팅

빌드 0/0 후:
- 트리 노드 **더블클릭/F2** → 인라인 편집, Enter 반영 / 빈값 거부 / Esc 취소
- 트리 **우클릭** → 4개 메뉴, `이름 변경`·`종료`·`재시작`·`명령 실행`(커맨드 바 포커스) 동작

| 증상 | 원인 | 해결 |
|------|------|------|
| 편집 TextBox가 안 뜸/포커스 안 감 | `Loaded`로 포커스(1회만) | `IsVisibleChanged`(Step 5 ②) |
| 라벨과 편집칸 토글 안 됨 | 로컬 `Visibility` 지정이 트리거 이김 | 기본값을 스타일 Setter로(Step 4 ①, 07 ⑥) |
| 컨텍스트 메뉴 커맨드 비활성/미작동 | ContextMenu가 VM 못 찾음 | `PlacementTarget.DataContext`(Step 4) |
| 우클릭한 노드가 아닌 딴 노드에 작용 | 우클릭이 선택을 안 옮김 | `PreviewMouseRightButtonDown`에서 IsSelected(Step 5) |

---

## 다음 단계 / 참고

- **11**: 출력 캡처 버퍼·라우팅(FR-047).
- [Runbook 07](./07_wpf_multi_terminal_tabs.md)(트리·⑥ 우선순위) · [08](./08_wpf_command_injection_output_capture.md)(커맨드 바) · [09](./09_wpf_session_termination.md)(재시작)
- 기획: [`07_interfaces`](../plans/v1/07_interfaces.md)(SC-23) · [`04_requirements`](../plans/v1/04_requirements.md)(FR-046)
