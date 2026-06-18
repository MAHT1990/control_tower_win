# ViewModels 컨벤션

## 개요

**ViewModels**는 View와 Service 사이의 중개자다. NestJS의 `*.controller.ts`에 대응한다 —
사용자 입력을 받아 Service에 위임하고, 결과를 View가 바인딩할 수 있는 형태로 노출한다.

- 위치: `src/ControlTowerWin/Shell/ViewModels/` 또는 `Features/<기능>/ViewModels/`
- 책임: 바인딩 가능한 속성/명령(Command) 노출, View 상태 관리, Service 호출
- 핵심: `INotifyPropertyChanged`로 값 변경을 View에 자동 통지

---

## 구조

```
Shell/ViewModels/MainWindowViewModel.cs          # 기능 VM 조합
Features/SessionMonitor/ViewModels/SessionListViewModel.cs
Features/TerminalLauncher/ViewModels/NewTerminalViewModel.cs
```

공통 기반 클래스 `ViewModelBase`는 `Shared/Core/`에 둔다([shared.md](./shared.md)).

```
ViewModelBase (Shared/Core, INotifyPropertyChanged 구현)
   ▲
   ├─ SessionListViewModel   (Sessions 컬렉션 노출, ProcessTracker 구독)
   ├─ NewTerminalViewModel   (OpenCommand 노출)
   └─ MainWindowViewModel    (위 VM들을 속성으로 보유)
```

---

## 사용 패턴

### 현재 → 목표

현재 `MainWindow`가 직접 들고 있는 `Terminals` 컬렉션과 추적 로직이 `SessionListViewModel`로 이동한다.

**Before (현재)** — Window가 컬렉션·타이머·이벤트를 모두 보유
```csharp
public ObservableCollection<TerminalInfo> Terminals { get; } = new();
private readonly DispatcherTimer _timer;   // 폴링도 여기
```

**After (목표)** — VM이 컬렉션 노출, 추적은 Service에 위임
```csharp
public class SessionListViewModel : ViewModelBase
{
    public ObservableCollection<TerminalInfo> Sessions { get; } = new();

    public SessionListViewModel(ProcessTracker tracker)
    {
        tracker.SessionAdded   += s => Sessions.Add(s);     // Service 이벤트 구독
        tracker.SessionRemoved += pid => Remove(pid);
        tracker.Start();
    }
}
```

### Command 패턴 (버튼 → 메서드)

이벤트 핸들러(`Click=`) 대신 `ICommand`를 노출해 View와 분리한다.
`RelayCommand`는 `Shared/Core/`에 둔다.

```csharp
public ICommand OpenCommand { get; }
public NewTerminalViewModel(TerminalLauncher launcher)
{
    OpenCommand = new RelayCommand(_ => launcher.OpenNewWindow());
}
```

### 속성 변경 통지

```csharp
private string _status = "";
public string Status
{
    get => _status;
    set { _status = value; OnPropertyChanged(); }   // ViewModelBase 제공
}
```

---

## 주의사항

1. **UI 스레드 전환**: Service가 백그라운드 스레드에서 이벤트를 쏘면(예: `Process.Exited`),
   `ObservableCollection` 수정은 UI 스레드에서 해야 한다. VM 경계에서 `Dispatcher`로 전환하거나
   `SynchronizationContext`를 캡처해 처리한다. (현재 코드의 `Dispatcher.Invoke` 책임이 여기로 이동)
2. **VM은 WPF View 타입을 모른다**: VM에서 `Window`, `Button` 등을 직접 참조하지 않는다.
   화면 전환·다이얼로그는 서비스/이벤트로 추상화.
3. **VM-First 매핑**: 셸이 VM 인스턴스를 `ContentControl.Content`에 넣으면, 어떤 View를 그릴지
   `DataTemplate`로 알려줘야 한다.
   ```xml
   <DataTemplate DataType="{x:Type vm:SessionListViewModel}">
       <views:SessionListView/>
   </DataTemplate>
   ```
4. **생성자 주입**: VM은 필요한 Service를 생성자로 받는다(DI 친화). 내부에서 `new ProcessTracker()`로
   직접 만들면 테스트·교체가 어려워진다.
