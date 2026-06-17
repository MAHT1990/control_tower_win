# Runbook 01 — WPF Hello World 프로젝트 생성

## 목표
Visual Studio 2026에서 WPF 프로젝트를 처음부터 손으로 만들고,
화면에 "Hello World"를 출력하는 앱을 완성한다.

---

## 사전 조건
- [ ] Visual Studio 2026 설치 완료
- [ ] 워크로드: **.NET 데스크톱 개발** 체크 확인

---

## Step 1. 새 프로젝트 생성

1. Visual Studio 2026 실행
2. 시작 화면에서 **새 프로젝트 만들기** 클릭
3. 검색창에 `WPF` 입력
4. **WPF 애플리케이션** 선택 (언어: C#, 플랫폼: Windows)
   - 주의: "WPF 앱(.NET Framework)"가 아닌 **"WPF 애플리케이션"** 선택 (.NET 8+ 기준)
5. **다음** 클릭

---

## Step 2. 프로젝트 구성

| 항목 | 값 |
|------|-----|
| 프로젝트 이름 | `HelloWorldWpf` |
| 위치 | `C:\Users\kitor\Projects\control_tower_win\src` |
| 솔루션 이름 | `HelloWorldWpf` |
| .NET 버전 | .NET 8.0 (또는 설치된 최신) |

- **솔루션 및 프로젝트를 같은 디렉터리에 배치** 체크 해제 (기본값 유지)
- **만들기** 클릭

---

## Step 3. 자동 생성 파일 구조 확인

솔루션 탐색기에서 아래 구조가 보여야 정상:

```
HelloWorldWpf/
 ├─ App.xaml
 ├─ App.xaml.cs
 ├─ MainWindow.xaml
 ├─ MainWindow.xaml.cs
 └─ HelloWorldWpf.csproj
```

각 파일 역할:

| 파일 | 역할 |
|------|------|
| `App.xaml` | 앱 진입점, 전역 리소스 정의 |
| `App.xaml.cs` | 앱 시작 이벤트 처리 |
| `MainWindow.xaml` | 메인 창 UI 레이아웃 (XML 기반) |
| `MainWindow.xaml.cs` | 메인 창 코드 비하인드 (이벤트·로직) |

---

## Step 4. MainWindow.xaml 수정

`MainWindow.xaml`을 더블클릭하여 열고, 기존 내용을 아래로 교체한다.

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

### 핵심 요소 설명

| 요소 | 설명 |
|------|------|
| `Window` | 창 자체. `Title`=창 제목표시줄, `Height`/`Width`=초기 크기 |
| `Grid` | WPF 기본 레이아웃 패널. 행/열 분할 가능 |
| `TextBlock` | 텍스트 표시 전용 컨트롤 |
| `HorizontalAlignment="Center"` | 가로 중앙 정렬 |
| `VerticalAlignment="Center"` | 세로 중앙 정렬 |
| `FontSize="32"` | 폰트 크기 (단위: device-independent pixel) |

---

## Step 5. 빌드 및 실행

1. 상단 메뉴 → **빌드 > 솔루션 빌드** (단축키: `Ctrl+Shift+B`)
2. 오류 없이 완료되면 **디버그 > 디버깅 시작** (단축키: `F5`)
3. 창 중앙에 **"Hello World"** 텍스트가 출력되면 성공

---

## 체크리스트

- [ ] Step 1: 프로젝트 생성 완료
- [ ] Step 2: 프로젝트 구성 설정 완료
- [ ] Step 3: 파일 구조 4개 확인
- [ ] Step 4: MainWindow.xaml 수정 완료
- [ ] Step 5: 빌드 성공, 화면에 "Hello World" 출력 확인

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| WPF 템플릿이 목록에 없음 | 워크로드 미설치 | VS Installer → .NET 데스크톱 개발 추가 |
| 빌드 오류: `namespace not found` | 프로젝트 이름과 `x:Class` 불일치 | `x:Class="HelloWorldWpf.MainWindow"` 확인 |
| 창은 뜨는데 텍스트 없음 | `Text` 속성 누락 | `TextBlock`에 `Text="Hello World"` 확인 |
| XAML 디자이너 안 보임 | .NET 데스크톱 개발 워크로드 미포함 | VS Installer → 수정 → 워크로드 추가 |
