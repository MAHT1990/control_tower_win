# Runbook 13 — 터미널 글꼴 설정: 전역 설정·라이브 재적용 (FR-048 / SC-02 / FN-SYS-06)

> **방식**: 직접 구현·검증 후 역산출. 코드는 `.NET 10 / net10.0-windows`에서 빌드 0/0·스모크 검증 완료.
> **🔤 C# 문법 짚기**: 각 Step 끝에 새 문법 박스(06~12에서 다룬 건 반복하지 않음).

---

## 목표

임베드 터미널의 **글꼴(크기·종류)을 전역 설정**으로 조정한다.

1. **설정 화면(SC-02)**: 글꼴 크기(6~72pt 자유 수치)·종류(monospace 한정 목록) 편집.
2. **원자 영속**: `%APPDATA%\ControlTowerWin\settings.json`에 temp→rename 저장(NFR-011). 재시작 후 유지.
3. **라이브 재적용**: 저장 즉시 **실행 중 전 터미널**에 반영(세션 유지, 재시작 불요) + 신규 터미널에 승계.

> 기획 근거: FR-048(글꼴 설정)·FN-SYS-06·SC-02 확장·ENT-015(terminal_font_*)·RISK-011.
> 줄간격/셀 spacing은 엔진(Microsoft.Terminal.Wpf) 미지원 확인 → v1 제외(후속 숙제 ⑨).

---

## 전제

- [ ] Runbook 08~09 완료 — `ITerminalSession` 엔진 경계(주입/캡처/수명)·`EasyTerminalSession`
- [ ] Runbook 07 keep-alive pane 구조

---

## 개념 (큰 틀)

### 1. 엔진의 글꼴 적용 경로 — 이 런북의 핵심 함정 (RISK-011)

EasyTerminalControl의 README는 "폰트 속성 변경 후 `SetTheme`를 호출하라"고 하지만, **WPF 컨트롤의 `SetTheme`는 private**라 외부에서 호출할 수 없다. 실소스 기준 실제 경로는:

```csharp
// 컨트롤 내부 (EasyTerminalControl.cs)
public TerminalTheme? Theme { set => SetTheme(_Theme = value); private get => _Theme; }
private void SetTheme(TerminalTheme? v) {
    if (v != null)   // ★ null이면 no-op!
        Terminal?.SetTheme(v.Value, FontFamilyWhenSettingTheme.Source, (short)FontSizeWhenSettingTheme);
}
```

즉 **유일한 재적용 레버 = write-only `Theme` 프로퍼티에 non-null `TerminalTheme` 재대입**이다. 함정 2개:

- **함정 1 (null-theme no-op)**: 우리 `TerminalView.xaml`은 Theme를 설정한 적이 없어 `_Theme = null`. 폰트 속성만 바꾸고 Theme를 안 건드리면(또는 null을 재대입하면) **조용히 무시**된다. → 앱이 **non-null 기본 `TerminalTheme`(색 5필드 + ColorTable[16])를 보유**하고 있다가 재대입해야 한다.
- **함정 2 (getter도 private)**: 이전 테마값을 컨트롤에서 되읽을 수 없다. → 기본 테마를 **앱 상수**(Campbell 팔레트)로 보유한다.

다행인 것: 이 경로는 **렌더러만 리테마**한다 — `ConPTYTerm`을 건드리지 않으므로 **pwsh 세션·스크롤백이 그대로 유지**된다(재시작 불요).

### 2. 적용 파이프라인 (전 터미널 + 신규 + 시작 시)

```
[설정 창 저장] ─FontSettingsChanged─▶ Sessions.ApplyFontToAll(family, size)
                                        ├─ 실행 중 전 터미널: session.ApplyFont(...)  (즉시 리테마)
                                        └─ _currentFont 보관 → 이후 신규 탭/터미널에 승계
[앱 시작] ─ store.Load() ─▶ ApplyFontToAll  (이때 세션은 아직 attach 전!)
                                        └─ TerminalViewModel._pendingFont에 보관
                                           → View 로드 → AttachSession 시점에 적용
```

**pending 메모지 패턴**: 앱 시작 직후·새 터미널 추가 직후에는 ConPTY 세션이 아직 없다(`AttachSession`은 View `Loaded` 시점). `TerminalViewModel.ApplyFont`가 값을 항상 `_pendingFont`에 보관하고, `AttachSession`에서 보관분을 적용하면 타이밍 문제가 사라진다.

### 3. monospace 열거 — WPF에 isFixedPitch가 없다

WPF managed API는 고정폭 여부를 직접 노출하지 않는다. → **대표 글리프('i','l','W','M','x',' ')의 advance width가 전부 같은지** 비교해 판정한다. 수백 패밀리 × 글리프 로드는 느릴 수 있어 **결과를 1회 캐시**한다.

---

## 아키텍처 통합 (Feature × Layer)

```
Features/Settings/                        # (신규 Feature 모듈)
├── Models/      AppSettings.cs           # ENT-015 부분(글꼴 2필드) + Normalize(CON-13)
├── Interfaces/  ISettingsStore.cs        # 영속 계약 — 항상 분리 규칙
├── Services/    JsonSettingsStore.cs     # %APPDATA% JSON + temp→rename(NFR-011)
│                MonospaceFontProvider.cs # 고정폭 열거(글리프 폭 비교·캐시)
├── ViewModels/  SettingsViewModel.cs     # 편집·검증(6~72)·저장·FontSettingsChanged 방송
└── Views/       SettingsWindow.xaml(.cs) # SC-02 모달(airspace-safe 별도 창)

Features/EmbeddedTerminal/
├── Interfaces/  ITerminalSession.cs      # (수정) + ApplyFont(family, size)
├── Services/    EasyTerminalSession.cs   # (수정) + ApplyFont 구현·기본 TerminalTheme 상수
└── ViewModels/  TerminalViewModel.cs     # (수정) + pending 글꼴
                 TerminalSessionsViewModel.cs # (수정) + ApplyFontToAll·신규 승계

Shell/           MainWindowViewModel.cs   # (수정) 시작 시 적용 + 설정 VM 팩토리(방송 배선)
                 MainWindow.xaml(.cs)     # (수정) 설정 버튼 → 모달 오픈
```

> Settings가 EmbeddedTerminal을 직접 참조하지 않는다 — **Shell이 이벤트로 조합**(Feature 간 직접 참조 0, NFR-016). 엔진 함정(TerminalTheme·write-only Theme)은 전부 `EasyTerminalSession` 1곳에 캡슐화(NFR-018).

---

## Step 1. ITerminalSession — 글꼴 계약 확장

`Features/EmbeddedTerminal/Interfaces/ITerminalSession.cs` — 09의 계약에 추가:

```csharp
    /* 런타임 글꼴 적용(FR-048). 렌더러만 리테마하여 ConPTY 세션은 유지된다. */
    void ApplyFont(string fontFamily, int fontSize);
```

---

## Step 2. EasyTerminalSession — 기본 테마 보유 + 리테마

`Features/EmbeddedTerminal/Services/EasyTerminalSession.cs` — using에 `System.Windows.Media`·`Microsoft.Terminal.Wpf` 추가 후:

```csharp
    /* 글꼴 재적용에 필수인 non-null 기본 테마(Campbell 팔레트).
       컨트롤의 SetTheme는 private·Theme getter도 private라, 앱이 테마 값을 보유했다가
       write-only Theme에 재대입해야 내부 SetTheme가 현재 글꼴로 리테마한다(null이면 no-op). */
    private static readonly TerminalTheme DefaultTheme = new()
    {
        DefaultBackground = 0x0C0C0C,
        DefaultForeground = 0xCCCCCC,
        DefaultSelectionBackground = 0xFFFFFF,
        CursorStyle = CursorStyle.BlinkingBar,
        ColorTable = new uint[]
        {
            0x0C0C0C, 0x1F0FC5, 0x0EA113, 0x009CC1,
            0xDA3700, 0x981788, 0xDD963A, 0xCCCCCC,
            0x767676, 0x5648E7, 0x0CC616, 0xA5F1F9,
            0xFF783B, 0x9E00B4, 0xD6D661, 0xF2F2F2,
        },
    };

    /* 런타임 글꼴 적용(FR-048). 렌더러만 리테마 — ConPTY 세션·스크롤백 유지. */
    public void ApplyFont(string fontFamily, int fontSize)
    {
        if (string.IsNullOrWhiteSpace(fontFamily) || fontSize <= 0) return;
        _control.FontFamilyWhenSettingTheme = new FontFamily(fontFamily);
        _control.FontSizeWhenSettingTheme = fontSize;
        _control.Theme = DefaultTheme;
    }
```

### 🔤 C# 문법 짚기 (Step 2)

**① COLORREF (uint 0x00BBGGRR)**
```csharp
DefaultBackground = 0x0C0C0C,
```
- `TerminalTheme`의 색은 Win32 COLORREF — **BGR 순서**의 uint다(WPF Color의 ARGB와 다름). `0xDA3700`은 R=0x00,G=0x37,B=0xDA인 파란 계열. 위 16색은 Windows Terminal 기본 Campbell 팔레트.

**② write-only 프로퍼티에 대입 = 메서드 호출 트리거**
```csharp
_control.Theme = DefaultTheme;
```
- 겉보기는 단순 대입이지만, setter 안에서 내부 `SetTheme`가 실행돼 **렌더러 리테마라는 부작용**이 일어난다. "값 저장"이 아니라 "행동 트리거"로 쓰이는 프로퍼티 — 이 컨트롤 API의 특성이다.

---

## Step 3. TerminalViewModel — pending 글꼴 (타이밍 해결)

`Features/EmbeddedTerminal/ViewModels/TerminalViewModel.cs` — `AttachSession`을 교체하고 `ApplyFont` 추가:

```csharp
    /* 세션 attach 전에 요청된 글꼴(신규 터미널·앱 시작 시). attach 시점에 적용된다. */
    private (string Family, int Size)? _pendingFont;

    public void AttachSession(ITerminalSession session)
    {
        _session = session;
        if (_pendingFont is { } font)
        {
            session.ApplyFont(font.Family, font.Size);
        }
    }

    /* 글꼴 적용(FR-048). 세션 미준비면 보관했다가 attach 시 적용. */
    public void ApplyFont(string fontFamily, int fontSize)
    {
        _pendingFont = (fontFamily, fontSize);
        _session?.ApplyFont(fontFamily, fontSize);
    }
```

### 🔤 C# 문법 짚기 (Step 3)

**① 튜플 타입 `(string Family, int Size)?`**
```csharp
private (string Family, int Size)? _pendingFont;
```
- 두 값을 임시 클래스 없이 **이름 붙은 쌍**으로 담는다. 뒤의 `?`는 "아직 없음(null)"도 표현 — "보관된 글꼴이 있거나 없거나"를 한 필드로.

**② 속성 패턴 `is { } x` (null 검사 + 꺼내기)**
```csharp
if (_pendingFont is { } font)
```
- "null이 아니면 그 값을 `font`로 받아라". nullable 튜플을 null 검사와 동시에 언래핑하는 현대적 관용구(`is not null` + 변수 캡처의 축약).

---

## Step 4. TerminalSessionsViewModel — 전 터미널 방송 + 신규 승계

```csharp
    /* 현재 전역 글꼴(FR-048). 신규 탭/터미널에 승계된다. */
    private (string Family, int Size)? _currentFont;

    /* 전역 글꼴을 전 터미널에 적용(FR-048/FN-SYS-06). 이후 생성 터미널에도 승계. */
    public void ApplyFontToAll(string fontFamily, int fontSize)
    {
        _currentFont = (fontFamily, fontSize);
        foreach (var terminal in Tabs.SelectMany(t => t.Terminals))
        {
            terminal.ApplyFont(fontFamily, fontSize);
        }
    }
```

신규 터미널·신규 탭 생성 경로에 승계를 끼운다:
- `AddTerminalCommand`의 실행을 `AddTerminalToSelected()`로 바꿔 `SelectedTab?.AddTerminal()` 반환값에 `_currentFont` 적용.
- `AddTab()` 끝에서 `tab.Terminals` 전체에 `_currentFont` 적용(탭 생성자가 첫 터미널을 만들기 때문).

---

## Step 5. Features/Settings — 모델·영속·폰트 열거

**`Models/AppSettings.cs`** — ENT-015 부분(글꼴 2필드) + `Normalize()`(빈 family→기본, 범위 밖 size→기본; CON-13 CHECK 6..72).

**`Interfaces/ISettingsStore.cs`** — `Load()`(부재·손상 시 기본값 graceful) / `Save(settings)`(원자 커밋).

**`Services/JsonSettingsStore.cs`** — 경로 `%APPDATA%\ControlTowerWin\settings.json`:

```csharp
    public void Save(AppSettings settings)
    {
        settings.Normalize();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, _path, overwrite: true);   // 동일 볼륨 NTFS = 원자 교체(NFR-011)
    }
```

**`Services/MonospaceFontProvider.cs`** — 고정폭 판정 + 캐시:

```csharp
    public static IReadOnlyList<string> GetMonospaceFamilies()
    {
        return _cache ??= Fonts.SystemFontFamilies
            .Where(IsMonospace)
            .Select(f => f.Source)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /* 대표 글자들의 advance width가 전부 같으면 고정폭으로 판정 */
    private static bool IsMonospace(FontFamily family)
    {
        foreach (var typeface in family.GetTypefaces())
        {
            if (!typeface.TryGetGlyphTypeface(out var glyph)) continue;
            double? width = null;
            foreach (var ch in ProbeChars)   // 'i','l','W','M','x',' '
            {
                if (!glyph.CharacterToGlyphMap.TryGetValue(ch, out var index)) return false;
                double advance = glyph.AdvanceWidths[index];
                if (width is null) width = advance;
                else if (Math.Abs(advance - width.Value) > 1e-6) return false;
            }
            return width != null;
        }
        return false;
    }
```

### 🔤 C# 문법 짚기 (Step 5)

**① null 병합 대입 `??=` (1회 캐시)**
```csharp
return _cache ??= Fonts.SystemFontFamilies...ToList();
```
- "`_cache`가 null이면 우변을 계산해 대입하고, 이미 있으면 그대로 반환". 느린 열거(수백 폰트 × 글리프 로드)를 앱 수명 동안 1번만 수행하는 캐시를 한 줄로.

**② `GlyphTypeface.AdvanceWidths` (글리프 전진폭)**
- 폰트의 각 글리프가 차지하는 **가로 폭**(em 단위). `CharacterToGlyphMap`으로 문자→글리프 인덱스를 얻어 폭을 조회한다. 'i'(좁은 글자)와 'W'(넓은 글자)의 폭이 같다면 그 폰트는 고정폭이다.

---

## Step 6. SettingsViewModel + SettingsWindow (SC-02)

**`ViewModels/SettingsViewModel.cs`** — 핵심 설계:
- `FontSizeText`를 **문자열**로 받아 `IsValid`(파싱 + 6~72 범위)와 `ValidationMessage`를 파생 — 숫자 아닌 입력도 인라인 메시지로 처리.
- `SaveCommand`(CanExecute=IsValid) → 원자 저장 → **`FontSettingsChanged(family, size)` 방송**.
- 콤보 목록은 `MonospaceFontProvider` + 현재 저장값이 목록에 없으면 맨 앞에 삽입(사라진 폰트 보호).

**`Views/SettingsWindow.xaml(.cs)`** — 모달 창(airspace 무관한 별도 Window). 글꼴 종류 ComboBox·크기 TextBox(6~72)·검증 메시지·저장/취소. 코드비하인드에서:

```csharp
        viewModel.FontSettingsChanged += (_, _) => Close();   // 저장 성공 시 닫기
```

취소는 `IsCancel="True"` 버튼이 자동 처리.

---

## Step 7. Shell 조합 — 시작 시 적용 + 방송 배선

**`Shell/ViewModels/MainWindowViewModel.cs`**:

```csharp
    public MainWindowViewModel()
    {
        _settingsStore = new JsonSettingsStore();
        Sessions = new TerminalSessionsViewModel();

        /* 시작 시 저장된 글꼴 적용(FR-048 재시작 유지 AC) — 세션 attach 전이라 pending으로 보관됐다가 적용 */
        var settings = _settingsStore.Load();
        Sessions.ApplyFontToAll(settings.TerminalFontFamily, settings.TerminalFontSize);
    }

    /* 설정 창용 VM 생성 — 저장 방송을 전 터미널 라이브 재적용에 배선(FN-SYS-06) */
    public SettingsViewModel CreateSettingsViewModel()
    {
        var viewModel = new SettingsViewModel(_settingsStore);
        viewModel.FontSettingsChanged += Sessions.ApplyFontToAll;
        return viewModel;
    }
```

**`Shell/Views/MainWindow.xaml`** — 헤더에 설정 버튼(`Click="OnSettingsClick"`), 코드비하인드:

```csharp
    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var window = new SettingsWindow(vm.CreateSettingsViewModel()) { Owner = this };
        window.ShowDialog();
    }
```

### 🔤 C# 문법 짚기 (Step 7)

**메서드 그룹을 이벤트에 직접 구독**
```csharp
viewModel.FontSettingsChanged += Sessions.ApplyFontToAll;
```
- `FontSettingsChanged`는 `Action<string, int>`, `ApplyFontToAll(string, int)`은 시그니처가 일치 → 람다 없이 **메서드 이름 그대로** 구독한다(메서드 그룹 변환). Settings feature와 EmbeddedTerminal feature가 서로 모른 채 **Shell에서 한 줄로 결선**되는 지점.

---

## Step 8. 빌드 및 검증

```powershell
cd src/ControlTowerWin
dotnet build ControlTowerWin.csproj
```

1. 빌드 **경고 0 / 오류 0**.
2. **설정 로드 스모크(자동)**: `%APPDATA%\ControlTowerWin\settings.json`을 ①없음 ②무효값(`size:999`) ③손상 JSON ④유효값 4상태로 두고 각각 기동 → 전부 크래시 없이 기동(Normalize·graceful 검증).
3. **육안 검증**(탐색기 더블클릭 실행):
   - [설정] → 글꼴 크기 16, 종류 변경 → [저장] → **열려 있는 모든 터미널이 즉시** 새 글꼴로 (세션·스크롤백 유지 = RISK-011 PoC)
   - 크기에 5나 100 입력 → 인라인 거부 메시지 + 저장 버튼 비활성
   - [새 터미널] → 새 pane도 같은 글꼴
   - 앱 재시작 → 저장한 글꼴로 시작 (재시작 유지 AC)

---

## 체크리스트

- [ ] Step 1: `ITerminalSession.ApplyFont` 계약
- [ ] Step 2: `EasyTerminalSession` — 기본 `TerminalTheme` 상수(Campbell) + `Theme` 재대입 리테마
- [ ] Step 3: `TerminalViewModel` — `_pendingFont` 보관·attach 시 적용
- [ ] Step 4: `TerminalSessionsViewModel` — `ApplyFontToAll`·신규 탭/터미널 승계
- [ ] Step 5: `Features/Settings` — AppSettings(Normalize)·JsonSettingsStore(원자)·MonospaceFontProvider(캐시)
- [ ] Step 6: SettingsViewModel(검증·방송) + SettingsWindow(모달)
- [ ] Step 7: Shell — 시작 시 적용 + `FontSettingsChanged → ApplyFontToAll` 배선 + 설정 버튼
- [ ] Step 8: 빌드 0/0 + 4상태 스모크 + 육안(라이브 반영·세션 유지·범위 거부·신규 승계·재시작 유지)

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| **글꼴을 바꿔도 아무 일 없음** | `Theme`가 null(미설정)이라 내부 SetTheme가 no-op | Step 2처럼 non-null 기본 `TerminalTheme`를 재대입(RISK-011) |
| `SetTheme` 호출 시 컴파일 오류 | WPF 컨트롤의 SetTheme는 private(README와 다름) | `Theme` 프로퍼티 재대입으로 트리거 |
| 글꼴 변경 후 색이 바뀜 | 재대입한 테마가 기존과 다른 색 | 앱 보유 기본 테마(Campbell)를 일관 사용 — getter가 private라 되읽기 불가 |
| 시작 시/새 터미널에 글꼴 미적용 | 세션 attach 전에 ApplyFont 호출됨 | pending 보관 → `AttachSession`에서 적용(Step 3) |
| 콤보 목록이 늦게 뜸 | 폰트 열거가 느림(수백 패밀리 글리프 로드) | `??=` 1회 캐시(Step 5). 필요시 앱 시작 시 백그라운드 워밍업 |
| 저장한 글꼴이 목록에 없어 콤보가 빈 값 | 폰트 제거·이름 변경 | 현재 저장값을 목록 맨 앞에 삽입(Step 6) |
| 줄간격 설정이 없음 | 엔진(Microsoft.Terminal.Wpf) 표면 미노출 | 정상 — v1 제외, 후속 숙제 ⑨ |

---

## 다음 단계 / 참고

- 후속: 줄간격/셀 spacing(숙제 ⑨, 엔진 확장·폴백 시) · 색 테마 사용자 설정(DefaultTheme 필드화로 자연 확장) · 프로파일별 글꼴 오버라이드.
- [Runbook 08](./08_wpf_command_injection_output_capture.md)(ITerminalSession 경계) · [09](./09_wpf_session_termination.md)(수명)
- 기획: [`04_requirements`](../plans/v1/04_requirements.md)(FR-048) · [`07_interfaces`](../plans/v1/07_interfaces.md)(SC-02) · [`09_database`](../plans/v1/09_database.md)(ENT-015·CON-13) · [`10_tech`](../plans/v1/10_tech.md)(TS-02 폰트 경로·RISK-011)
- [EasyWindowsTerminalControl (GitHub)](https://github.com/mitchcapper/EasyWindowsTerminalControl) — EasyTerminalControl.cs Theme/SetTheme
