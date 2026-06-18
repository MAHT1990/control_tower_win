# Models 컨벤션

## 개요

**Models**는 앱이 다루는 **순수 데이터**를 정의하는 레이어다. NestJS의 dto/entity에 대응한다.
UI도 비즈니스 로직도 없이, 데이터의 모양만 규정한다.

- 위치: `src/ControlTowerWin/Features/<기능>/Models/`
- 책임: 데이터 구조 + 표시용 파생 속성
- 예: 터미널 세션 정보(`TerminalInfo`), 커맨드 요청(`CommandRequest`)

---

## 구조

현재 `MainWindow.xaml.cs`에 인라인으로 선언된 `TerminalInfo`가 대표 Model이다.

```csharp
// 현재: MainWindow.xaml.cs:17
public class TerminalInfo
{
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName => $"[{ProcessName}] PID {Pid}";   // 표시용 파생 속성
}
```

→ 목표 위치: `Features/SessionMonitor/Models/TerminalInfo.cs`
(`namespace ControlTowerWin.Features.SessionMonitor.Models`)

---

## 사용 패턴

### 파생 속성으로 표시 텍스트 캡슐화

`DisplayName`처럼 "원본 필드 조합으로 만든 표시값"은 Model의 계산 속성으로 둔다.
View가 직접 문자열을 조립하지 않게 하여 표현 일관성을 유지한다.

```csharp
public string DisplayName => $"[{ProcessName}] PID {Pid}";   // ListBox가 이걸 그대로 표시
```

### 불변 데이터는 record 고려

세션 식별처럼 한번 만들어지면 안 바뀌는 데이터는 `record`로 표현하면 의도가 분명해진다.

```csharp
public record CommandRequest(int TargetPid, string CommandText);
```

### Good / Bad

**Good** — Model은 데이터만
```csharp
public class TerminalInfo { public int Pid { get; set; } public string ProcessName { get; set; } }
```

**Bad** — Model이 프로세스를 직접 제어 (로직 침범)
```csharp
public class TerminalInfo
{
    public int Pid { get; set; }
    public void Kill() => Process.GetProcessById(Pid).Kill();  // ← Service로 가야 함
}
```

---

## 주의사항

1. **로직 금지**: Model에 프로세스 제어·파일 IO·네트워크 호출을 넣지 않는다. 그건 Services의 책임.
2. **UI 타입 금지**: `Brush`, `Visibility` 등 WPF 표현 타입을 Model에 두지 않는다.
   표현 변환은 Converter([shared.md](./shared.md)) 또는 ViewModel에서.
3. **알림이 필요하면 ViewModel로**: Model 값이 바뀔 때 UI 갱신이 필요하면
   `INotifyPropertyChanged`는 보통 ViewModel이 담당한다. Model은 단순 데이터로 유지.
4. **기능별 소속**: Model은 그 데이터를 주로 쓰는 기능 폴더에 둔다. 여러 기능이 공유하면 `Shared/`로.
