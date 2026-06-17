# Runbook 01 — WPF Hello World 프로젝트 생성

## 목표
Visual Studio 2026에서 WPF 프로젝트를 처음부터 손으로 만들고,
화면에 "Hello World"를 출력하는 앱을 완성한다.

---

## 개념

### WPF란?
WPF(Windows Presentation Foundation)는 Microsoft가 만든 **Windows 네이티브 UI 프레임워크**다.
화면 레이아웃은 XAML(XML 기반 마크업 언어)로 정의하고, 동작 로직은 C#으로 작성한다.

```
WPF 구조
  ┌──────────────────────────────────────┐
  │  MainWindow.xaml   ← UI 레이아웃    │
  │  MainWindow.xaml.cs ← 동작 로직     │
  │       ↕ (partial class로 연결)      │
  │  컴파일 → HelloWorldWpf.exe         │
  └──────────────────────────────────────┘
```

### XAML이란?
XAML(Extensible Application Markup Language)은 **UI를 선언적으로 표현하는 XML 기반 언어**다.
HTML이 웹 페이지의 구조를 정의하듯, XAML은 Windows 앱의 화면 구조를 정의한다.

```xml
<!-- HTML과의 비교 -->
<div style="text-align:center">Hello World</div>   ← HTML

<TextBlock HorizontalAlignment="Center"
           Text="Hello World" />                   ← XAML
```

### .NET과 C#의 관계

```
C# 소스코드
     ↓ 컴파일
  IL(중간 언어, CPU 무관한 중립 코드)
     ↓ 실행 시점 JIT 컴파일
  기계어 (각 플랫폼 CLR이 변환)
     ↓
  실행
```

- **C#** — IL을 만드는 언어
- **.NET(CLR)** — IL을 실행하는 플랫폼
- **WPF** — .NET 위에서 동작하는 UI 프레임워크

### 솔루션 vs 프로젝트

```
솔루션 (.slnx)          ← 여러 프로젝트를 묶는 컨테이너
 └─ 프로젝트 (.csproj)  ← 실제 앱 단위 (빌드 단위)
     ├─ MainWindow.xaml
     └─ ...
```

- **솔루션**: 프로젝트들의 묶음. 빌드·배포 단위를 조직하는 역할
- **프로젝트**: 실제로 빌드되어 `.exe` 또는 `.dll`이 되는 단위

VS 2026은 솔루션 파일로 기존 `.sln` 대신 **`.slnx`(XML 기반)** 형식을 사용한다.

### 코드 비하인드(Code-behind)란?
XAML 파일과 짝을 이루는 C# 파일이다.
`MainWindow.xaml` ↔ `MainWindow.xaml.cs` 가 한 쌍으로, `partial class`로 컴파일 시 합쳐진다.

```
MainWindow.xaml      → UI 구조 선언
MainWindow.xaml.cs   → 버튼 클릭, 데이터 처리 등 동작 정의
        ↓
   컴파일 시 하나의 MainWindow 클래스로 합쳐짐
```

---

## 사전 조건

- [ ] Visual Studio 2026 설치 완료
- [ ] 워크로드: **.NET 데스크톱 개발** 체크 확인
  - VS Installer → 설치된 VS 2026 → **수정** → 워크로드 탭에서 확인

---

## Step 1. 새 프로젝트 생성

1. Visual Studio 2026 실행
2. 시작 화면에서 **새 프로젝트 만들기** 클릭
3. 검색창에 `WPF` 입력
4. **WPF 애플리케이션** 선택 (언어: C#, 플랫폼: Windows)
   - ⚠️ "WPF 앱(.NET Framework)"가 아닌 **"WPF 애플리케이션"** 선택
5. **다음** 클릭

---

## Step 2. 프로젝트 구성

| 항목 | 값 |
|------|-----|
| 프로젝트 이름 | `HelloWorldWpf` |
| 위치 | `C:\Users\kitor\Projects\control_tower_win\src` |
| 솔루션 이름 | `HelloWorldWpf` |
| .NET 버전 | .NET 10.0 |

- **솔루션 및 프로젝트를 같은 디렉터리에 배치** → ⚠️ **반드시 체크** ✅
- **만들기** 클릭

> **왜 체크해야 하나?**
> 미체크 시 솔루션과 프로젝트가 중첩 구조로 생성되어 솔루션이 프로젝트를 참조하지 못한다.
> 결과적으로 **빌드 메뉴가 사라지고 디버그도 비활성화**된다.
> ```
> 미체크 시 (잘못된 구조)       체크 시 (올바른 구조)
> src/HelloWorldWpf/            src/HelloWorldWpf/
>  └─ HelloWorldWpf/             ├─ HelloWorldWpf.slnx
>      └─ HelloWorldWpf.csproj   └─ HelloWorldWpf.csproj
> ```

---

## Step 3. 자동 생성 파일 구조 확인

솔루션 탐색기에서 아래 구조가 보여야 정상:

```
HelloWorldWpf/
 ├─ App.xaml            ← 앱 진입점, 전역 리소스 정의
 ├─ App.xaml.cs         ← 앱 시작 이벤트 처리
 ├─ AssemblyInfo.cs     ← 어셈블리 메타데이터 (.NET 10 추가, 무시 가능)
 ├─ MainWindow.xaml     ← 메인 창 UI 레이아웃
 ├─ MainWindow.xaml.cs  ← 메인 창 코드 비하인드
 └─ HelloWorldWpf.csproj
```

---

## Step 4. MainWindow.xaml 수정

`MainWindow.xaml` 더블클릭 → 기존 내용을 아래로 교체 → `Ctrl+S` 저장

```xml
<Window x:Class="HelloWorldWpf.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Hello World" Height="200" Width="400">
    <Grid>
        <TextBlock Text="Hello World"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center"
                   FontSize="32" />
    </Grid>
</Window>
```

### 코드 해설

| 요소 / 속성 | 설명 |
|------------|------|
| `Window` | 창 자체. OS에 띄워지는 네이티브 윈도우 |
| `Title` | 창 제목표시줄 텍스트 |
| `Height` / `Width` | 초기 창 크기 (단위: DIP, 화면 해상도 무관) |
| `x:Class` | 이 XAML과 연결될 C# 클래스 지정 |
| `xmlns` | WPF 기본 네임스페이스 (컨트롤 정의 포함) |
| `xmlns:x` | XAML 언어 자체 기능 네임스페이스 |
| `Grid` | WPF 기본 레이아웃 패널. 행/열 분할 가능 |
| `TextBlock` | 텍스트 표시 전용 컨트롤 (편집 불가) |
| `HorizontalAlignment="Center"` | Grid 안에서 가로 중앙 배치 |
| `VerticalAlignment="Center"` | Grid 안에서 세로 중앙 배치 |
| `FontSize="32"` | 폰트 크기 (DIP 단위) |

---

## Step 5. 빌드 및 실행

1. `Ctrl+Shift+B` — 솔루션 빌드
2. 빌드 성공 후 `F5` — 디버깅 시작
3. 창 중앙에 **"Hello World"** 가 출력되면 완료

---

## 체크리스트

- [ ] Step 1: WPF 애플리케이션 템플릿 선택
- [ ] Step 2: "같은 디렉터리에 배치" 체크 확인 후 만들기
- [ ] Step 3: 파일 5개 구조 확인
- [ ] Step 4: MainWindow.xaml 수정 및 저장
- [ ] Step 5: 빌드 성공, 화면에 "Hello World" 출력 확인

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| WPF 템플릿이 목록에 없음 | 워크로드 미설치 | VS Installer → .NET 데스크톱 개발 추가 |
| 빌드 메뉴가 없고 디버그 비활성화 | "같은 디렉터리에 배치" 미체크로 중첩 구조 생성 | 프로젝트 삭제 후 Step 2 옵션 체크하고 재생성 |
| 빌드 오류: `namespace not found` | 프로젝트 이름과 `x:Class` 불일치 | `x:Class="HelloWorldWpf.MainWindow"` 확인 |
| 창은 뜨는데 텍스트 없음 | `Text` 속성 누락 | `TextBlock`에 `Text="Hello World"` 확인 |
| XAML 디자이너 안 보임 | .NET 데스크톱 개발 워크로드 미포함 | VS Installer → 수정 → 워크로드 추가 |
| `.sln` 파일이 안 보임 | VS 2026은 `.slnx` 형식 사용 | 정상, `.slnx`가 솔루션 파일임 |
