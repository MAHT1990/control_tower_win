# Views 컨벤션

## 개요

**Views**는 화면에 보이는 것 — XAML과 그 코드비하인드를 담는 레이어다.
NestJS에는 직접 대응이 없지만, "사용자에게 노출되는 표면"으로 controller의 표현 부분에 해당한다.

- 위치: `src/ControlTowerWin/Shell/Views/`(셸) 또는 `Features/<기능>/Views/`(기능)
- 책임: 레이아웃·바인딩 선언. **로직은 최소화**, 데이터는 `{Binding}`으로 ViewModel에서 끌어온다.
- 형태: `*.xaml` + `*.xaml.cs`(코드비하인드)

---

## 구조

```
Shell/Views/MainWindow.xaml(.cs)              # 셸 윈도우
Features/SessionMonitor/Views/SessionListView.xaml(.cs)   # 기능 View (UserControl)
```

현재 `MainWindow.xaml`은 3행 Grid(제목 / 세션목록 ListBox / 버튼)로 구성된다.
목표 구조에서 가운데 ListBox 부분은 `SessionListView`(UserControl)로 분리된다.

```
MainWindow.xaml (Shell)
├─ Row0: "Control Tower" 제목
├─ Row1: <ContentControl Content="{Binding SessionList}"/>  → SessionListView
└─ Row2: New Terminal 버튼  → TerminalLauncher
```

---

## 사용 패턴

### x:Class 와 namespace 는 폴더 경로와 일치

View를 폴더로 옮기면 `x:Class`(XAML)와 코드비하인드 `namespace`를 **동시에** 갱신한다.

```xml
<!-- Features/SessionMonitor/Views/SessionListView.xaml -->
<UserControl x:Class="ControlTowerWin.Features.SessionMonitor.Views.SessionListView" ...>
```
```csharp
// Features/SessionMonitor/Views/SessionListView.xaml.cs
namespace ControlTowerWin.Features.SessionMonitor.Views { public partial class SessionListView ... }
```

### Window → UserControl 분리

기능 화면은 `Window`가 아니라 `UserControl`로 만들어 셸의 영역(`ContentControl`)에 끼운다.

**Before (현재)** — 모든 UI가 MainWindow 한 장
```xml
<Window ...>
  <ListBox ItemsSource="{Binding Terminals}" DisplayMemberPath="DisplayName"/>
  <Button Click="OnNewTerminalClick"/>
</Window>
```

**After (목표)** — 기능 View로 쪼갬 + Command 바인딩
```xml
<!-- SessionListView.xaml (UserControl) -->
<ListBox ItemsSource="{Binding Sessions}" DisplayMemberPath="DisplayName"/>
```
```xml
<!-- MainWindow.xaml -->
<ContentControl Content="{Binding SessionList}"/>
<Button Command="{Binding NewTerminal.OpenCommand}"/>   <!-- Click 핸들러 → Command -->
```

### 코드비하인드 최소화

`Click="OnNewTerminalClick"` 같은 이벤트 핸들러보다 `Command="{Binding ...}"`를 우선한다.
코드비하인드는 InitializeComponent 정도만 남기는 것이 MVVM 지향.

---

## 주의사항

1. **DataContext 설정**: View가 바인딩하려면 `DataContext`가 해당 ViewModel이어야 한다.
   셸은 `DataContext = mainWindowViewModel`, 기능 View는 `ContentControl.Content`로 VM을 받으면
   DataTemplate(아래)로 자동 연결된다.
2. **VM↔View 매핑**: 폴더가 분산되면 자동 매핑 규칙이 깨지므로, `App.xaml` 또는 셸 리소스에
   `DataTemplate`로 VM→View를 명시한다. 상세는 [viewmodels.md](./viewmodels.md) "VM-First 매핑".
3. **DisplayMemberPath 유지**: 단순 목록은 `DisplayMemberPath="DisplayName"`(Model 파생 속성)로
   충분하다. 항목 UI가 복잡해지면 `ItemTemplate`으로 전환.
4. **이동 시 빌드 확인**: x:Class/namespace 불일치는 런타임이 아니라 빌드에서 잡힌다. 옮긴 뒤 즉시 빌드.
