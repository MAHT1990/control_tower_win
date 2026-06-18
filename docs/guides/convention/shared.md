# Shared 컨벤션

## 개요

**Shared**는 여러 기능이 공통으로 쓰는 횡단(cross-cutting) 자산을 모은다.
NestJS의 `common` / `SharedModule`에 대응한다. 의존 그래프의 최하위 — 아무것도 참조하지 않고
모두에게 참조된다.

- 위치: `src/ControlTowerWin/Shared/`
- 책임: 기반 클래스, 재사용 컨트롤, 값 변환기, 공유 스타일
- 원칙: 특정 기능에 종속되지 않는 것만 둔다. 한 기능 전용이면 그 기능 폴더에.

---

## 구조

```
Shared/
├── Core/         ViewModelBase.cs      # INotifyPropertyChanged 기반 클래스
│                 RelayCommand.cs       # ICommand 간편 구현
├── Controls/     (재사용 UserControl — VM 없는 순수 UI)
├── Converters/   (IValueConverter 모음 — 예: BoolToVisibility)
└── Styles/       (공유 ResourceDictionary — Brush, Style)
```

네임스페이스: `ControlTowerWin.Shared.Core`, `ControlTowerWin.Shared.Converters` 등.

---

## 사용 패턴

### ViewModelBase — 모든 VM의 부모

```csharp
// Shared/Core/ViewModelBase.cs
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

### RelayCommand — 버튼 바인딩용 ICommand

```csharp
// Shared/Core/RelayCommand.cs
public class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? p) => canExecute?.Invoke(p) ?? true;
    public void Execute(object? p) => execute(p);
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
```

### Converter — 표현 변환을 한 곳에

```csharp
// Shared/Converters/BoolToVisibilityConverter.cs
public class BoolToVisibilityConverter : IValueConverter { ... }
```
```xml
<!-- 공유 스타일/리소스로 등록해 모든 View에서 재사용 -->
<conv:BoolToVisibilityConverter x:Key="BoolToVis"/>
```

### Good / Bad — 무엇을 Shared에 둘 것인가

**Good** — 어느 기능에도 종속 없는 범용 자산
```
Shared/Core/ViewModelBase.cs        # 모든 VM이 상속
Shared/Converters/BoolToVisibility  # 어느 화면에서나 쓰임
```

**Bad** — 한 기능 전용인데 Shared에 둠
```
Shared/SessionListViewModel.cs      # ← Features/SessionMonitor/ViewModels/ 로 가야 함
```

---

## 주의사항

1. **단방향 의존**: Shared는 `Features/`나 `Shell/`을 참조하지 않는다. 역방향(그들이 Shared 참조)만 허용.
   이 규칙이 깨지면 순환 의존이 생긴다.
2. **승격 기준**: 처음엔 한 기능 안에 있던 것이 두 번째 기능에서도 필요해지면 그때 Shared로 올린다.
   "혹시 쓸까봐" 미리 올리지 않는다(YAGNI).
3. **Styles 병합**: 공유 `ResourceDictionary`는 `App.xaml`의 `Application.Resources`에 병합해
   전역 적용한다.
4. **Core는 프레임워크 성격**: `ViewModelBase`/`RelayCommand`는 사실상 미니 MVVM 프레임워크다.
   규모가 커지면 CommunityToolkit.Mvvm 등 표준 라이브러리로 대체를 검토한다.
