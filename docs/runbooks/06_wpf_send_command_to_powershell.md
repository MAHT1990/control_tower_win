# Runbook 06 — 앱 내 임베드 터미널 (ConPTY · PoC + 최소 임베드)

## 목표

앱 창 **안에 살아있는 PowerShell 터미널**을 띄운다. 사람이 직접 타이핑하면 그대로 동작하는,
진짜 터미널(ConPTY 기반)을 WPF 영역에 임베드한다.

- 이번 런북(06): 라이브러리 PoC 검증 + **최소 임베드**(pwsh 렌더 + 사람 타이핑)까지
- 다음(06b): 앱에서 프로그램적 **명령 주입** + **출력 가로채기/로깅**
- 다음(07): 임베드 세션 **종료/수명 관리**

> 본 런북은 **실제로 구현·검증한 절차를 그대로 기록**한 것이다. 특히 "빌드는 되는데 터미널이 검정"이 되는
> native 배포 함정을 [트러블슈팅](#트러블슈팅)에서 상세히 다룬다 — 이게 이번 작업의 핵심 교훈이다.

---

## 개념

### 왜 ConPTY인가 (방법 A·B의 한계)

`claude`·`python` 같은 인터랙티브/TUI 프로그램은 `isatty()`로 **진짜 터미널(PTY)** 을 요구한다.
단순 파이프로는 raw 모드·키 이벤트·전체화면 렌더가 안 된다.

```
방법 A (stdin 리다이렉트)   → 사람 입력 차단, TUI 불가
방법 B (WriteConsoleInput)  → 출력 못 읽음, conhost 강제
방법 C (ConPTY)             → 화면 + 사람입력 + 앱입력 + claude/python + 출력읽기 모두 충족 ✅
```

### ConPTY는 "자식을 직접 spawn" — 외부 세션엔 못 붙는다

ConPTY는 이미 떠 있는 외부 `powershell` 프로세스에 **부착할 수 없다**. 반드시 ConPTY가 **자식 프로세스를
직접 띄워야** 한다. 그래서 이 임베드 터미널은 `SessionMonitor`(외부 `wt` 세션 목록)와 **별개의 신규 패널**이다.

```
SessionMonitor (기능 1)        EmbeddedTerminal (이번 기능, 별개)
─────────────────────          ──────────────────────────────
외부 wt/pwsh 프로세스 추적·표시   앱이 직접 spawn한 pwsh를 앱 안에서 렌더
(주입 불가 — 남의 프로세스)        (앱이 부모 — 입력·출력 제어 가능)
```

> Runbook 05의 우클릭 `명령 실행`은 "외부 세션에 주입"을 떠올리게 하지만, ConPTY로는 불가능하다.
> 임베드 터미널은 그와 무관한 **앱 자신의 세션**이다.

### EasyWindowsTerminalControl = Windows Terminal 백엔드를 임베드

직접 ConPTY 플러밍을 짜면 `claude` 같은 풀스크린 TUI 렌더가 별도 대공사다. 그래서 **공식 Windows Terminal
렌더러를 그대로 쓰는 NuGet 컨트롤**을 임베드한다.

```
사람 타이핑 ─▶ EasyTerminalControl ─▶ conpty.dll ─▶ OpenConsole.exe ─▶ powershell.exe
powershell ─출력─▶ OpenConsole ─▶ conpty.dll ─▶ Microsoft.Terminal.Control(렌더) ─▶ 화면
            (06b: 앱이 TermPTY.WriteToTerm로 같은 입력 경로에 명령 주입)
```

- **주의(beta 의존)**: 이 컨트롤은 아직 공개 패키징되지 않은 Windows Terminal의 beta 패키지
  (`CI.Microsoft.Terminal.Wpf`, `CI.Microsoft.Windows.Console.ConPTY`)에 의존한다. 저수준 API가 바뀔 수 있다.
- **airspace 한계**: 컨트롤이 native HwndHost라, 터미널 **위에** 일반 WPF 요소를 겹쳐 그릴 수 없다
  (컨텍스트 메뉴 등은 OK). WebView2와 같은 제약.

### PoC 게이트 — "라이브러리가 .NET 10에서 정말 도나"를 먼저 검증

beta 의존 + .NET 10 명시 지원 미문서화 상태라, 정식 구현 전에 **싸게 먼저 확인**한다:
패키지가 restore/build 되고, 임베드 컨트롤이 실제로 pwsh를 렌더하는가? **GO면 진행, NO-GO면 폴백**
([폴백 — MS GUIConsole.ConPTY](#폴백--ms-guiconsoleconpty-no-go-시)).

### native 배포 — 이번 런북의 핵심 함정 (미리보기)

이 컨트롤은 **C/C++ native 부품**에 의존한다:
- `Microsoft.Terminal.Control.dll` — 렌더러(native)
- `conpty.dll` — 가짜 콘솔(앱이 P/Invoke로 호출)
- `OpenConsole.exe` — ConPTY가 띄우는 PTY 호스트

그런데 **`conpty.dll`·`OpenConsole.exe`는 .NET SDK 앱에 자동 복사되지 않는다**(아래 Step 1·트러블슈팅).
이걸 빠뜨리면 **빌드는 멀쩡한데 터미널만 검정**으로 뜬다.

---

## 사전 조건

- [ ] Runbook 04 완료 — `Shell / Features / Shared` 하이브리드 구조
- [ ] .NET 10 SDK 설치 (`dotnet --version` → `10.x`)
- [ ] **Windows 10 build 17763(RS4) 이상** — ConPTY 최소 요건
- [ ] `nuget.org` 패키지 소스 등록 (Runbook 02 사전 조건) — beta 패키지 restore에 필수
- [ ] `powershell.exe`(Windows 내장) — 임베드 터미널이 띄울 셸. (`pwsh`·`cmd`·`claude` 등으로 교체 가능)

---

## Phase 0 — PoC 게이트 (라이브러리 검증)

정식 구현 전에 "되는지"만 싸게 확인한다.

> 아래 `dotnet add package`는 **버려도 되는 스모크 체크**다. csproj에 버전 없는 `<PackageReference>` 한 줄을
> 자동 추가할 뿐이다. **Step 1에서 csproj를 (버전 핀 + native 복사 포함) 전체 교체**하므로, 여기서 추가된
> 줄은 곧 덮어쓰인다. 놀라지 말 것.

```powershell
cd src/ControlTowerWin
dotnet add package EasyWindowsTerminalControl
dotnet build ControlTowerWin.csproj
```

확인 포인트 + 실측 결과:

| 검증 | 기대 | 실측(net10.0-windows / SDK 10.0.301) |
|------|------|------|
| restore가 패키지+beta transitive 해결 | NU 오류 없음 | ✅ `EasyWindowsTerminalControl 1.0.36` + `CI.Microsoft.Terminal.Wpf 1.22.250204002` + `CI.Microsoft.Windows.Console.ConPTY 1.22.250314001` 해결 |
| 패키지 TFM 호환 | net10 호환 | ✅ "모든 프레임워크와 호환됩니다". 패키지 lib은 net6/net8만 제공 → **net10 프로젝트가 net8.0-windows7.0을 고르는 것이 정상** |
| 빌드 | 오류 0 | ✅ (단, 경고 2개 → Step 1에서 해소) |

**GO 판정**: 위 3개 통과 → 아래 Step으로 진행.
**NO-GO**(TFM 미해결 / 빌드 오류 / 이후 렌더 완전 실패): → [폴백](#폴백--ms-guiconsoleconpty-no-go-시).

> 이 시점에 실행하면 **터미널이 검정**일 것이다. 정상이다 — Step 1의 native 배포가 빠졌기 때문.

---

## Step 1. 패키지 + csproj (RID · UseRidGraph · native 복사)

`ControlTowerWin.csproj` **전체를 아래 내용으로 교체**한다 — Phase 0의 `dotnet add package`가 자동 추가한
`<PackageReference>` 줄도 이 내용으로 덮어쓴다. **native 복사 블록(맨 아래 `<ItemGroup>`)이 이 런북의 핵심**이며,
빠뜨리면 빌드는 되어도 [터미널이 검정](#트러블슈팅)이 된다.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <UseRidGraph>true</UseRidGraph>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="EasyWindowsTerminalControl" Version="1.0.36" />
    <!-- 전이 의존을 직접 참조로 고정 + 경로 프로퍼티 생성 (native 자산 복사 경로 확보) -->
    <PackageReference Include="CI.Microsoft.Windows.Console.ConPTY" Version="1.22.250314001" GeneratePathProperty="true" />
  </ItemGroup>

  <!-- ConPTY native 자산을 출력으로 복사 (패키지가 .NET 앱용 복사 타겟 미제공).
       conpty.dll = 가짜 콘솔(P/Invoke 대상), OpenConsole.exe = PTY 호스트 -->
  <ItemGroup>
    <None Include="$(PkgCI_Microsoft_Windows_Console_ConPTY)\runtimes\win10-x64\native\conpty.dll"
          Link="conpty.dll" CopyToOutputDirectory="PreserveNewest" Visible="false" />
    <None Include="$(PkgCI_Microsoft_Windows_Console_ConPTY)\build\native\runtimes\x64\OpenConsole.exe"
          Link="OpenConsole.exe" CopyToOutputDirectory="PreserveNewest" Visible="false" />
  </ItemGroup>

</Project>
```

### 한 줄씩 의미

- `RuntimeIdentifier=win-x64` — 컨트롤의 native DLL이 AMD64다. 미지정 시 빌드 경고 `MSB3270`
  (MSIL↔AMD64 아키텍처 불일치) + 런타임 오류. win-x64로 정렬하면 `Microsoft.Terminal.Control.dll`
  (`runtimes/win-x64/native/`)이 자동 복사된다.
- `UseRidGraph=true` — `conpty.dll`이 **구버전 RID `win10-x64`** 칸에 들어 있다. .NET 8+ 기본 RID 그래프는
  버전별 RID를 해석하지 않아 경고 `NETSDK1206`이 뜬다. 옛 RID 그래프를 켜서 해석되게 한다.
- `GeneratePathProperty=true` — NuGet이 `$(PkgCI_Microsoft_Windows_Console_ConPTY)`(패키지 캐시 경로)
  프로퍼티를 생성한다. 하드코딩 경로 없이 native 파일을 가리키기 위함.
- `<None ... CopyToOutputDirectory>` 2줄 — **이게 검정 화면의 해결책**.
  `conpty.dll`(`runtimes/win10-x64/native/`)과 `OpenConsole.exe`(`build/native/runtimes/x64/`)를
  출력 폴더로 직접 복사한다. 둘 다 native 프로젝트(C++)용 위치라 .NET 앱엔 자동 복사되지 않는다.
  게다가 EasyWindowsTerminalControl이 두 `CI.Microsoft.*` 의존(ConPTY·Terminal.Wpf)을 모두 `exclude="Build,Analyzers"`로
  끌어와, 패키지가 제공하는 자동 복사 **build 타겟마저 전이 단계에서 차단**된다 → **수동 복사가 (편의가 아니라) 구조적으로 필수**다.

> 검증: 빌드 후 `bin/Debug/net10.0-windows/win-x64/`에 `conpty.dll`·`OpenConsole.exe`·
> `Microsoft.Terminal.Control.dll`·`Microsoft.Terminal.Wpf.dll`·`EasyWindowsTerminalControl.dll`이
> **모두** 있어야 한다(이 5개가 핵심 — `ControlTowerWin.exe`·`.pdb`·`deps.json` 등이 함께 있어도 정상). 빌드 경고는 0개가 된다.

---

## Step 2. EmbeddedTerminal — 폴더 + View

먼저 새 기능 폴더를 만든다: `Features/EmbeddedTerminal/Views/`, `Features/EmbeddedTerminal/ViewModels/`.
(`Models/Interfaces/Services`는 06에서 만들지 않는다 — 넣을 게 없다. 점진적 승격.)

`Features/EmbeddedTerminal/Views/TerminalView.xaml` (UserControl, 컨트롤 호스팅)

```xml
<UserControl x:Class="ControlTowerWin.Features.EmbeddedTerminal.Views.TerminalView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:term="clr-namespace:EasyWindowsTerminalControl;assembly=EasyWindowsTerminalControl">
    <!-- 앱이 직접 spawn하는 임베드 터미널 (ConPTY) -->
    <term:EasyTerminalControl StartupCommandLine="powershell.exe"/>
</UserControl>
```

- 컨트롤 정식 타입은 **`EasyWindowsTerminalControl.EasyTerminalControl`** (패키지명과 클래스명이 다르다).
  XmlnsDefinition URI가 없으므로 **`clr-namespace:...;assembly=...`** 형식으로 xmlns를 건다.
- `StartupCommandLine` — 띄울 콘솔 앱. 기본값도 `"powershell.exe"`. `pwsh.exe`·`cmd.exe`·`claude` 등으로 교체 가능.
  (06b에서 VM 바인딩으로 끌어올릴 수 있다.)

`Features/EmbeddedTerminal/Views/TerminalView.xaml.cs` (코드비하인드 — 최소)

```csharp
using System.Windows.Controls;

namespace ControlTowerWin.Features.EmbeddedTerminal.Views;

public partial class TerminalView : UserControl
{
    public TerminalView() => InitializeComponent();
}
```

---

## Step 3. EmbeddedTerminal — ViewModel (최소 앵커)

`Features/EmbeddedTerminal/ViewModels/TerminalViewModel.cs`

```csharp
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

/// <summary>
/// 임베드 터미널 패널의 ViewModel.
/// 06 단계에서는 View(EasyTerminalControl) 호스팅을 위한 VM-First 앵커 역할만 한다.
/// 프로그램적 명령 주입(WriteToTerm)·출력 가로채기는 Runbook 06b에서 이 VM에 추가한다.
/// </summary>
public class TerminalViewModel : ViewModelBase
{
}
```

> 06 단계 VM은 비어 있다 — Shell이 `DataTemplate`로 View를 찾게 하는 **앵커**다.
> `Models/Interfaces/Services`는 만들지 않는다(점진적 승격). 주입 계약(`Interfaces/`)은 06b에서 도입한다.

---

## Step 4. Shell 조합

`Shell/ViewModels/MainWindowViewModel.cs` — 기능 VM에 임베드 터미널 VM 추가

```csharp
using ControlTowerWin.Features.EmbeddedTerminal.ViewModels;
using ControlTowerWin.Features.SessionMonitor.Services;
using ControlTowerWin.Features.SessionMonitor.ViewModels;
using ControlTowerWin.Features.TerminalLauncher.Services;
using ControlTowerWin.Features.TerminalLauncher.ViewModels;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Shell.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public SessionListViewModel SessionList { get; }
    public NewTerminalViewModel NewTerminal { get; }
    public TerminalViewModel Terminal { get; }

    public MainWindowViewModel()
    {
        SessionList = new SessionListViewModel(new ProcessTracker());
        NewTerminal = new NewTerminalViewModel(new TerminalLauncher());
        Terminal = new TerminalViewModel();
    }
}
```

`Shell/Views/MainWindow.xaml` — VM→View 매핑 + 가운데를 2열(세션목록 | 터미널)로 분할

```xml
<Window x:Class="ControlTowerWin.Shell.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:smvm="clr-namespace:ControlTowerWin.Features.SessionMonitor.ViewModels"
        xmlns:smv="clr-namespace:ControlTowerWin.Features.SessionMonitor.Views"
        xmlns:etvm="clr-namespace:ControlTowerWin.Features.EmbeddedTerminal.ViewModels"
        xmlns:etv="clr-namespace:ControlTowerWin.Features.EmbeddedTerminal.Views"
        Title="Control Tower" Height="600" Width="900">
    <Window.Resources>
        <!-- VM-First 매핑: SessionListViewModel을 그릴 때 SessionListView 사용 -->
        <DataTemplate DataType="{x:Type smvm:SessionListViewModel}">
            <smv:SessionListView/>
        </DataTemplate>
        <!-- VM-First 매핑: TerminalViewModel을 그릴 때 TerminalView 사용 -->
        <DataTemplate DataType="{x:Type etvm:TerminalViewModel}">
            <etv:TerminalView/>
        </DataTemplate>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="Control Tower"
                   HorizontalAlignment="Center" Margin="10" FontSize="32"/>

        <!-- 가운데 영역: 좌측 세션 목록 + 우측 임베드 터미널 -->
        <Grid Grid.Row="1" Margin="10,0,10,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="240"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <ContentControl Grid.Column="0" Content="{Binding SessionList}"/>
            <ContentControl Grid.Column="1" Margin="10,0,0,0" Content="{Binding Terminal}"/>
        </Grid>

        <!-- Click 핸들러 대신 Command 바인딩 -->
        <Button Grid.Row="2" Content="New Terminal Window" Margin="10"
                Command="{Binding NewTerminal.OpenCommand}"/>
    </Grid>
</Window>
```

```
┌──────────────────── Control Tower ────────────────────┐
│                      제목                              │  Row0 Auto
├───────────────┬────────────────────────────────────────┤
│ 세션 목록      │   임베드 터미널 (powershell)           │  Row1 *
│ (240px)       │   PS C:\...> _                          │
│ [pwsh] PID..  │   (사람 타이핑 가능)                    │
├───────────────┴────────────────────────────────────────┤
│              [ New Terminal Window ]                   │  Row2 Auto
└────────────────────────────────────────────────────────┘
```

---

## Step 5. 빌드 및 실행 검증

```powershell
cd src/ControlTowerWin
dotnet build ControlTowerWin.csproj
```

1. 빌드: **경고 0 / 오류 0**
2. 출력 폴더 **루트**(`bin/Debug/net10.0-windows/win-x64/`, 하위 `x64\` 폴더가 아님)에
   `conpty.dll`·`OpenConsole.exe`가 있는지 확인(없으면 검정 화면)
3. 실행: **탐색기에서 exe 더블클릭** 또는 **Visual Studio F5**
   (`bin/Debug/net10.0-windows/win-x64/ControlTowerWin.exe`)
4. 오른쪽 영역에 **PowerShell 프롬프트**가 렌더되는지
5. 영역 클릭 후 `Get-ChildItem` 입력 → Enter → 결과가 나오는지(인터랙티브)

> ⚠️ Git Bash 등에서 백그라운드로 띄우면 콘솔 컨텍스트 차이로 동작이 달라질 수 있다.
> **정상 데스크톱 실행(더블클릭/F5)으로 검증**할 것.

---

## 체크리스트

- [ ] Phase 0: `dotnet add package` 후 restore/build 통과(GO) — 이 단계의 csproj는 곧 Step 1이 교체
- [ ] Step 1: csproj **전체 교체** — `RuntimeIdentifier=win-x64`, `UseRidGraph=true`
- [ ] Step 1: ConPTY 패키지 직접참조(`GeneratePathProperty`) + `conpty.dll`·`OpenConsole.exe` 복사 `<None>`
- [ ] Step 2: `Features/EmbeddedTerminal/{Views,ViewModels}/` 폴더 생성 후 `TerminalView.xaml(.cs)` 작성
- [ ] Step 3: `TerminalViewModel`(ViewModelBase 상속, 최소 앵커)
- [ ] Step 4: `MainWindowViewModel`에 `Terminal` 보유 + `DataTemplate` + 2열 배치
- [ ] Step 5: 빌드 0/0, 출력 루트에 native 2종 존재, 실행 시 pwsh 렌더 + 타이핑

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| **빌드는 되는데 터미널이 검정/빈 화면** | `conpty.dll`·`OpenConsole.exe` native가 출력에 없음 → 컨트롤이 conpty를 P/Invoke 못 함 → 셸 미기동 | Step 1의 `<None>` 복사 2줄 추가. 출력 **루트**에 두 파일이 들어가는지 확인 |
| 검정인데 실행 방법(Bash/더블클릭)만 바꿔봄 | 실행 컨텍스트는 원인이 아님 | native 누락이 진짜 원인. Step 1 확인 |
| Step 1 적용했는데 `<PackageReference>`가 중복 | Step 1을 전체 교체가 아니라 추가로 붙임(Phase 0 줄 잔존) | csproj 전체를 Step 1 내용으로 덮어쓰기 |
| 빌드 경고 `MSB3270` (MSIL↔AMD64) | 컨트롤 native가 AMD64인데 프로젝트가 AnyCPU | `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` |
| 빌드 경고 `NETSDK1206` (win10-x64 등 RID 자산 못 찾음) | `conpty.dll`이 구버전 RID(win10-x64)에 있음 | `<UseRidGraph>true</UseRidGraph>` |
| (위 두 경고가 안 보임) | Step 1 csproj가 이미 두 프로퍼티를 포함 → 경고가 사전 해소됨 | 정상. 두 줄을 빼야 경고가 재현됨 |
| restore 실패: `win-x64`/beta 패키지 못 찾음 | `nuget.org` 소스 미등록 | Runbook 02 사전 조건대로 `nuget.org` 추가 |
| `$(Pkg...)` 경로가 빈 값 | ConPTY 패키지가 직접 참조가 아님 / `GeneratePathProperty` 누락 | 직접 `PackageReference` + `GeneratePathProperty="true"` |
| XAML 빌드 오류: `EasyTerminalControl`/네임스페이스 못 찾음 | 클래스명·xmlns 오타 | 타입은 `EasyTerminalControl`, xmlns는 `clr-namespace:EasyWindowsTerminalControl;assembly=EasyWindowsTerminalControl` |
| 터미널 위에 WPF 요소가 안 겹쳐짐 | airspace(native HwndHost) 한계 | 정상. 터미널 위 오버레이는 불가(컨텍스트 메뉴는 OK) |
| `claude`/`python` TUI 깨짐 | 폰트/VT | `FontFamilyWhenSettingTheme`(기본 Cascadia Code) 확인 |

---

## 폴백 — MS GUIConsole.ConPTY (NO-GO 시)

`EasyWindowsTerminalControl`이 .NET 10에서 끝내 안 되면(렌더 완전 실패·native 해결 불가), beta 의존 없이
**Microsoft 공식 샘플**로 직접 ConPTY를 굴린다.

- 소스: [microsoft/terminal `samples/ConPTY/GUIConsole`](https://github.com/microsoft/terminal/tree/main/samples/ConPTY/GUIConsole)
  - `GUIConsole.ConPTY`(netstandard2.0 P/Invoke 래퍼) + `GUIConsole.WPF`(WPF 호스트)
- API: `CreatePseudoConsole` + 입출력 익명 파이프 + `STARTUPINFOEX`, `WriteToPseudoConsole(string)`
- 트레이드오프: 공식·beta 무 / NuGet 미패키징(소스 편입 필요), VT100 렌더는 직접 처리 → 손이 더 감

> 본 프로젝트는 **GO**(EasyWindowsTerminalControl로 렌더·타이핑 성공)라 폴백은 미사용. 기록용.

---

## 리스크·미결 항목

- **라이선스 미확인**: 배포(Runbook 02 ClickOnce) 전 `EasyWindowsTerminalControl` repo LICENSE 확인 필수.
- **beta 버전 드리프트**: `CI.Microsoft.*` 패키지는 CI 피드. 버전을 **정확히 핀**해 두었다(드리프트 차단).
- **ClickOnce 배포**: `conpty.dll`·`OpenConsole.exe`가 self-contained 산출물에 동봉되는지 게시 후 확인.
- **개념 경계**: 임베드 터미널 ≠ SessionMonitor. 목록의 외부 세션엔 주입하지 않는다.

---

## 다음 단계

- **06b**: `TermPTY.WriteToTerm`로 앱→터미널 **명령 주입**, `InterceptOutputToUITerminal`/
  `LogConPTYOutput`+`GetConsoleText()`로 **출력 가로채기/로깅**. 이때 주입 계약을
  `Features/EmbeddedTerminal/Interfaces/`에 도입([interfaces.md](../guides/convention/interfaces.md)).
- **07**: 임베드 세션 **종료/수명** 관리(`RestartTerm`/`DisconnectConPTYTerm`), 별건으로 Runbook 05
  컨텍스트 메뉴 `종료`(외부 프로세스 Kill) 연결.

---

## 참고

- [구조 가이드 INDEX](../guides/INDEX.md) · [Runbook 04 구조화](./04_wpf_directory_structuring.md) ·
  [Runbook 05 컨텍스트 메뉴](./05_wpf_session_context_menu.md) · [Runbook 02 ClickOnce/NuGet](./02_wpf_publish_clickonce.md)
- [Creating a Pseudoconsole session — MS Learn](https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session)
- [EasyWindowsTerminalControl (GitHub)](https://github.com/mitchcapper/EasyWindowsTerminalControl) ·
  [NuGet](https://www.nuget.org/packages/easywindowsterminalcontrol/)
- [microsoft/terminal — ConPTY GUIConsole 샘플](https://github.com/microsoft/terminal/tree/main/samples/ConPTY/GUIConsole)
- [.NET RID 사용 가이드 (UseRidGraph/NETSDK1206)](https://aka.ms/dotnet/rid-usage)
