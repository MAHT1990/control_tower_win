# Runbook 11 — 터미널 간 출력 캡처 버퍼·라우팅(FR-047 / SC-24)

> **방식**: 직접 구현·검증 후 역산출. 코드는 빌드 0/0·실행 검증 완료(커밋 `ece83e6`).
> **🔤 C# 문법 짚기**: 각 Step 끝에 새 문법 박스.

---

## 목표

한 세션(A)의 출력을 **다른 세션(B/다수)에서 활용**한다 — 제품 1차 정체성(세션 간 오케스트레이션).

- **mechanism (c)**: A 출력을 앱 **캡처 버퍼**에 수집 → 사용자가 **편집(가공)** → 대상 세션 입력으로 **주입**.
- IPC 파일 채널과 별개의 **raw 출력 라우팅**. 08의 `CaptureOutput()`(GetConsoleText)을 소스로 쓴다.

```
[A 세션 출력] ──캡처──▶ [앱 캡처 버퍼(편집 가능)] ──편집──▶ ──주입──▶ [B 입력(들)]
```

> 기획 근거: FR-047(출력 라우팅)·SC-24(캡처 버퍼 패널)·FN-SES-11·ENT-019(캡처 버퍼, 런타임).

---

## 전제

- [ ] Runbook 08 완료 — `TerminalViewModel.CaptureOutput()`(출력 캡처), `InjectToTargets`(주입), 다중 대상 체크박스

---

## 개념 (큰 틀)

- **캡처 버퍼 = ENT-019**(런타임·비영속): `SourceAs`(출처)·`Content`(편집 가능)·`HasContent`.
- **airspace 정합**: 캡처 버퍼는 터미널 native 존 **밖** 별도 패널(하단 Row)에 둔다.
- **주입 경로 재사용**: 08의 `InjectToTargets(text)`를 그대로 써 커맨드 바와 라우팅이 **같은 대상 규칙**(체크된 다중 대상 / 선택 터미널)을 공유.

---

## Step 1. OutputCaptureBufferViewModel (ENT-019)

`ViewModels/OutputCaptureBufferViewModel.cs` (신규)

```csharp
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

/// <summary>
/// 세션 간 출력 라우팅용 캡처 버퍼 (FR-047 / ENT-019, 런타임·비영속).
/// A 세션 출력을 수집(Capture)하여 사용자가 편집한 뒤, 대상 세션 입력으로 주입한다.
/// </summary>
public class OutputCaptureBufferViewModel : ViewModelBase
{
    private string _sourceAs = string.Empty;
    private string _content = string.Empty;

    public string SourceAs
    {
        get => _sourceAs;
        set { _sourceAs = value; OnPropertyChanged(); }
    }

    public string Content
    {
        get => _content;
        set { _content = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasContent)); }
    }

    public bool HasContent => !string.IsNullOrEmpty(_content);

    public void Capture(string sourceLabel, string content)
    {
        SourceAs = sourceLabel;
        Content = content;
    }
}
```

---

## Step 2. TerminalSessionsViewModel — 캡처·라우팅 커맨드

```csharp
    public ICommand CaptureCommand { get; }
    public ICommand RouteCommand { get; }

    /* 출력 캡처 버퍼(FR-047/ENT-019) */
    public OutputCaptureBufferViewModel CaptureBuffer { get; } = new();

    /* 생성자:
    CaptureCommand = new RelayCommand(_ => Capture(), _ => SelectedTab?.SelectedTerminal != null);
    RouteCommand   = new RelayCommand(_ => InjectToTargets(CaptureBuffer.Content),
                                      _ => CaptureBuffer.HasContent && HasInjectTargets());
    */

    /* 선택 터미널의 현재 출력을 캡처 버퍼에 수집 */
    private void Capture()
    {
        var terminal = SelectedTab?.SelectedTerminal;
        if (terminal is null) return;
        CaptureBuffer.Capture(terminal.Title, terminal.CaptureOutput());
    }
```

> `RouteCommand`는 08의 `InjectToTargets(text)`를 재사용 — 버퍼 내용을 체크된 대상(없으면 선택 터미널)에 독립 주입. 커맨드 바와 완전히 같은 대상 규칙.

### 🔤 C# 문법 짚기 (Step 2)

**파생 속성 통지 — `OnPropertyChanged(nameof(HasContent))`**
```csharp
set { _content = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasContent)); }
```
- `HasContent`는 `Content`에서 계산되는 값이다. `Content`가 바뀌면 `HasContent`도 바뀐 것이므로 **함께 통지**해야 버튼 활성화(`RouteCommand` CanExecute)가 갱신된다. 한 속성 변화가 다른 파생 속성에 파급될 때의 관용 패턴.

---

## Step 3. TerminalSessionsView.xaml — SC-24 하단 패널 + 그리드 재구성

기존 2열 Grid를 **Row 0(트리+pane) / Row 1(캡처 버퍼)** 로 감싼다.

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <!-- Row 0: 기존 좌 트리 + 우 pane 분할 (2열 Grid) -->
    <Grid Grid.Row="0">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="220"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        <!-- ... 07/08/10의 좌 DockPanel + 우 ItemsControl ... -->
    </Grid>

    <!-- Row 1: SC-24 출력 캡처 버퍼 (터미널 존 밖 별도 패널) -->
    <Border Grid.Row="1" BorderBrush="#DDDDDD" BorderThickness="0,1,0,0" Margin="0,6,0,0" Padding="0,6,0,0">
        <DockPanel>
            <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="0,0,0,4">
                <TextBlock Text="출력 캡처 버퍼" FontWeight="Bold" VerticalAlignment="Center"/>
                <TextBlock Text="{Binding CaptureBuffer.SourceAs, StringFormat=' (from {0})'}"
                           VerticalAlignment="Center" Foreground="Gray"/>
                <Button Content="캡처" Command="{Binding CaptureCommand}" Margin="8,0,0,0" Padding="6,2"/>
                <Button Content="대상에 주입" Command="{Binding RouteCommand}" Margin="4,0,0,0" Padding="6,2"/>
            </StackPanel>
            <TextBox Text="{Binding CaptureBuffer.Content, UpdateSourceTrigger=PropertyChanged}"
                     AcceptsReturn="True" TextWrapping="Wrap" VerticalScrollBarVisibility="Auto"
                     Height="80" FontFamily="Consolas"/>
        </DockPanel>
    </Border>
</Grid>
```

### 🔤 C# 문법 짚기 (Step 3)

**① `StringFormat` 바인딩**
```xml
Text="{Binding CaptureBuffer.SourceAs, StringFormat=' (from {0})'}"
```
- 바인딩 값을 그대로 쓰지 않고 **틀에 끼워** 표시. `{0}` 자리에 `SourceAs`가 들어가 " (from tab1)"처럼 보인다. 코드 없이 XAML에서 간단 포맷.

**② 그리드 Row로 airspace 분리**
- 캡처 버퍼는 native 터미널 위에 겹칠 수 없으므로(airspace), 아예 **다른 Row**(하단)에 둔다. 터미널 존과 물리적으로 분리된 WPF 영역이라 자유롭게 그린다.

---

## Step 4. 빌드·검증 / 트러블슈팅

빌드 0/0 후:
- A 터미널에서 명령 실행(출력 생성) → A 선택 → 하단 **캡처** → 버퍼에 A 출력 + "(from A)"
- 버퍼 내용 **편집**(가공)
- B 터미널 체크(또는 선택) → **대상에 주입** → B에 버퍼 내용 주입 / 다중 체크 시 동시

| 증상 | 원인 | 해결 |
|------|------|------|
| 캡처가 빈 문자열 | 08의 `LogConPTYOutput` 조기 활성화 누락 | 08 Step 2(세션 생성자에서 켜기) 확인 |
| `대상에 주입` 비활성 | 버퍼 비었거나 대상 없음 | 캡처 후 + 대상 체크/선택 |
| 캡처가 콘솔 전체 텍스트 | GetConsoleText는 전체 반환 | (후속) 선택 영역/마지막 명령만 정밀 캡처 |

---

## 다음 단계 / 참고

- (후속) 캡처 정밀도(선택 영역·마지막 명령)·`captured_at`·라이브 파이프(mechanism b).
- [Runbook 08 캡처 소스](./08_wpf_command_injection_output_capture.md) · [10 컨텍스트 메뉴](./10_wpf_session_context_menu_rename.md)
- 기획: [`07_interfaces`](../plans/v1/07_interfaces.md)(SC-24) · [`04_requirements`](../plans/v1/04_requirements.md)(FR-047) · [`09_database`](../plans/v1/09_database.md)(ENT-019)
