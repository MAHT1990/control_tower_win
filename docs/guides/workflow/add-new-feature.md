# 새 기능 모듈 추가 워크플로우

## 개요

ControlTowerWin에 새 기능(예: "커맨드 전송")을 추가하는 표준 절차.
**레이어 역순**(가장 안쪽 Service → 바깥 View → Shell 등록)으로 만들면 의존성이 자연스럽게 채워진다.
작은 기능은 평면 배치로 시작하고, 파일이 늘면 레이어 폴더로 승격한다([feature-module.md](../convention/feature-module.md)).

예시 기능: `SendCommand` (Runbook 05 — 선택된 세션에 커맨드 주입)

---

## Step 1. 기능 폴더 생성

```
Features/SendCommand/
```
처음엔 폴더 하나만. 레이어 폴더는 파일이 쌓이면 만든다.

## Step 2. Interface + Service (가장 안쪽 — 계약·로직)

**계약은 `Interfaces/`에, 구현은 `Services/`에** 둔다(인터페이스는 1개여도 항상 분리 — [interfaces.md](../convention/interfaces.md)).

```csharp
// Features/SendCommand/Interfaces/IStdinInjector.cs
namespace ControlTowerWin.Features.SendCommand.Interfaces;
public interface IStdinInjector { void Send(int pid, string command); }
```
```csharp
// Features/SendCommand/Services/StdinInjector.cs
using ControlTowerWin.Features.SendCommand.Interfaces;   // 계약 참조
namespace ControlTowerWin.Features.SendCommand.Services;
public class StdinInjector : IStdinInjector
{
    public void Send(int pid, string command) { /* 방법 A: RedirectStandardInput */ }
}
```

## Step 3. Model (데이터 형태, 필요 시)

```csharp
// Features/SendCommand/Models/CommandRequest.cs
namespace ControlTowerWin.Features.SendCommand.Models;
public record CommandRequest(int TargetPid, string CommandText);
```

## Step 4. ViewModel (입력 수신 → Service 위임)

```csharp
// Features/SendCommand/ViewModels/SendCommandViewModel.cs
namespace ControlTowerWin.Features.SendCommand.ViewModels;
public class SendCommandViewModel : ViewModelBase   // Shared/Core
{
    private readonly IStdinInjector _injector;
    public string CommandText { get; set; } = "";
    public int TargetPid { get; set; }
    public ICommand SendCommand { get; }

    public SendCommandViewModel(IStdinInjector injector)
    {
        _injector = injector;
        SendCommand = new RelayCommand(_ => _injector.Send(TargetPid, CommandText));   // Shared/Core
    }
}
```

## Step 5. View (XAML — x:Class/namespace 폴더 일치)

```xml
<!-- Features/SendCommand/Views/SendCommandView.xaml -->
<UserControl x:Class="ControlTowerWin.Features.SendCommand.Views.SendCommandView" ...>
    <StackPanel>
        <TextBox Text="{Binding CommandText, UpdateSourceTrigger=PropertyChanged}"/>
        <Button Content="Send" Command="{Binding SendCommand}"/>
    </StackPanel>
</UserControl>
```

## Step 6. Shell 등록 (조합)

```csharp
// Shell/ViewModels/MainWindowViewModel.cs — 새 기능 VM 보유
public SendCommandViewModel SendCommand { get; }
```
```xml
<!-- App.xaml 또는 셸 리소스 — VM↔View DataTemplate 매핑 -->
<DataTemplate DataType="{x:Type vm:SendCommandViewModel}">
    <views:SendCommandView/>
</DataTemplate>
<!-- MainWindow.xaml — 영역에 배치 -->
<ContentControl Content="{Binding SendCommand}"/>
```

## Step 7. 빌드·실행 검증

```bash
dotnet build ControlTowerWin.csproj    # x:Class/namespace 정합성 확인
```
실행 후: 입력 → Send → 대상 세션에 커맨드가 들어가는지 확인.

---

## 체크리스트

- [ ] `Features/<기능>/` 폴더 생성 (평면 시작)
- [ ] Interface: 계약을 `Interfaces/`에 분리(1개여도 항상), namespace `...Interfaces`
- [ ] Service: 구현을 `Services/`에, `using ...Interfaces`로 계약 참조
- [ ] Model: 필요 시 데이터 record/class
- [ ] ViewModel: `ViewModelBase` 상속, Service 생성자 주입, `ICommand` 노출
- [ ] View: `UserControl`, x:Class/namespace = 폴더 경로
- [ ] Shell: VM 보유 + `DataTemplate` 매핑 + `ContentControl` 배치
- [ ] 파일 2개↑ 모인 레이어는 폴더로 승격
- [ ] 빌드 통과 + 실제 동작 확인
