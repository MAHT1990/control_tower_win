# Runbook 05 — 세션 항목 우클릭 컨텍스트 메뉴 (껍데기)

## 목표

`SessionListView`의 PowerShell 세션 목록에서 **각 항목을 우클릭하면 컨텍스트 메뉴가 뜨도록** 한다.
이번 런북은 **메뉴 프레임(껍데기)만** 만든다 — 실제 동작은 다음 런북에서 채운다.

- 런북 05 (이번): 우클릭 → 메뉴 팝업 등장 (항목은 비활성 placeholder)
- 런북 06: 메뉴에 **'명령 실행'** 동작 연결 (`Features/SendCommand/` 모듈)
- 런북 07: 메뉴에 **'종료'** 동작 연결

> 동작 없이 "메뉴가 뜬다"까지가 이번 목표다. 그래서 코드/로직(ViewModel·Service) 변경은 없고,
> **View(XAML) 한 파일만** 손댄다.

---

## 최종 결과 (이 런북 완료 시)

```
세션 목록에서 항목 위 우클릭
   │
   ▼
┌────────────────┐
│ 명령 실행   (비활성) │   ← 런북 06에서 활성화
│ 종료        (비활성) │   ← 런북 07에서 활성화
└────────────────┘
```

변경 파일: `Features/SessionMonitor/Views/SessionListView.xaml` **단 하나**.

---

## 개념

### ContextMenu — 우클릭으로 뜨는 떠다니는 메뉴

WPF의 `ContextMenu`는 컨트롤에 우클릭(또는 메뉴 키)을 했을 때 떠오르는 작은 메뉴다.
`Window`/`Grid`의 일반 자식이 아니라 **별도의 팝업(독립 창)** 으로 그려진다. 그래서:

- 메뉴 항목이 **하나도 없으면 아예 열리지 않는다**. → 껍데기라도 placeholder 항목 1개 이상 필요.
- 메뉴는 메인 화면과 **다른 비주얼 트리**에 있다. → 나중(06/07)에 항목에서 "어떤 PID를 클릭했는지"를
  알아내려면 `PlacementTarget`으로 원래 항목의 `DataContext`를 끌어와야 한다(이번엔 동작이 없어 생략).

### 어디에 붙일 것인가 — ListBox 전체 vs 각 항목

| 부착 위치 | 결과 |
|-----------|------|
| `<ListBox ...>` 자체에 `ContextMenu` | 목록의 **빈 공간**을 우클릭해도 뜸. 어떤 항목인지 불명확 |
| `ListBoxItem`(각 항목)에 `ContextMenu` | **항목 위**에서 우클릭해야 뜸. "이 세션에 대한 메뉴"가 명확 ✅ |

세션마다 명령을 보내거나 종료할 거라(06/07), **각 항목**에 붙이는 게 맞다.
항목 하나하나는 런타임에 `ListBoxItem`이라는 컨테이너로 자동 생성되므로,
직접 손댈 수 없다. 대신 **`ItemContainerStyle`** 로 "모든 `ListBoxItem`에 이 메뉴를 달아라"고 일괄 지정한다.

```
ListBox
 └─ ItemContainerStyle  (모든 항목 컨테이너에 적용되는 스타일)
      └─ Setter: ContextMenu = [명령 실행 / 종료]
           ▲
   각 ListBoxItem이 생성될 때 이 메뉴를 물고 태어남
```

---

## 사전 조건

- [ ] 런북 04 완료 — `SessionListView`에 세션 목록이 뜨고, 항목이 자동 추가/제거됨
- [ ] 앱 실행 시 PowerShell 세션이 목록에 보이는 상태

---

## Step 1. ItemContainerStyle 추가 — 각 항목에 메뉴 부착

`Features/SessionMonitor/Views/SessionListView.xaml`을 연다.
현재는 `ListBox`가 한 줄(self-closing)로 닫혀 있다.

**Before**
```xml
<ListBox ItemsSource="{Binding Sessions}" Height="120" DisplayMemberPath="DisplayName"/>
```

이걸 여는 태그/닫는 태그로 풀고, 안에 `ListBox.ItemContainerStyle`을 넣는다.

**After**
```xml
<ListBox ItemsSource="{Binding Sessions}" Height="120" DisplayMemberPath="DisplayName">
    <!-- 각 세션 항목(ListBoxItem)에 우클릭 컨텍스트 메뉴를 부착 -->
    <ListBox.ItemContainerStyle>
        <Style TargetType="ListBoxItem">
            <Setter Property="ContextMenu">
                <Setter.Value>
                    <ContextMenu>
                        <!-- 런북 06에서 동작 연결 예정 -->
                        <MenuItem Header="명령 실행" IsEnabled="False"/>
                        <!-- 런북 07에서 동작 연결 예정 -->
                        <MenuItem Header="종료" IsEnabled="False"/>
                    </ContextMenu>
                </Setter.Value>
            </Setter>
        </Style>
    </ListBox.ItemContainerStyle>
</ListBox>
```

### 한 줄씩 의미

- `ListBox.ItemContainerStyle` — "목록의 각 항목 컨테이너(`ListBoxItem`)에 적용할 스타일" 묶음.
- `Style TargetType="ListBoxItem"` — 이 스타일이 꾸밀 대상이 `ListBoxItem`임을 명시.
- `Setter Property="ContextMenu"` — 항목의 `ContextMenu` 속성에 값을 꽂는다.
- `Setter.Value` 안의 `<ContextMenu>` — 실제로 띄울 메뉴. 자식 `MenuItem`들이 메뉴 줄.
- `IsEnabled="False"` — 지금은 **회색 비활성**. 클릭해도 아무 일 없음(껍데기). 06/07에서 `Command`를 달며 활성화.

> 빈 `<ContextMenu></ContextMenu>`로 두면 우클릭해도 안 뜬다. placeholder 두 줄이 "메뉴가 뜬다"를
> 눈으로 확인하게 해 주는 최소 장치다.

---

## Step 2. 빌드 및 테스트

```powershell
cd src/ControlTowerWin
dotnet build ControlTowerWin.csproj
```

1. 빌드: 경고 0 / 오류 0
2. 실행(`dotnet run` 또는 F5)
3. 세션 목록의 **항목 위에서 우클릭** → `명령 실행` / `종료` 두 줄짜리 메뉴가 뜬다
4. 두 항목 모두 **회색(비활성)** 이고, 클릭해도 아무 동작이 없다 (정상 — 껍데기)
5. 항목이 **없는 빈 공간**에서 우클릭하면 메뉴가 안 뜬다 (각 항목에만 붙였으므로 정상)

---

## 체크리스트

- [ ] `SessionListView.xaml`의 `ListBox`를 여는/닫는 태그로 변경
- [ ] `ListBox.ItemContainerStyle` → `Style TargetType="ListBoxItem"` → `ContextMenu` Setter 추가
- [ ] `MenuItem` 2개(`명령 실행`, `종료`)를 `IsEnabled="False"`로 배치
- [ ] 빌드 통과(경고 0/오류 0)
- [ ] 항목 우클릭 시 메뉴 팝업 확인, 빈 공간 우클릭 시 안 뜸 확인

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| 우클릭해도 메뉴가 안 뜸 | `MenuItem`이 하나도 없음 → 빈 메뉴는 안 열림 | placeholder `MenuItem`을 최소 1개 둔다 |
| 빌드 오류: `ListBox`에 콘텐츠 직접 못 넣음 | self-closing(`/>`)을 닫는 태그로 안 풀고 자식 추가 | `<ListBox ...>` … `</ListBox>` 형태로 변경 |
| 메뉴가 항목이 아니라 목록 빈 곳에서도 뜸 | `ListBox` 자체에 `ContextMenu`를 달음 | `ItemContainerStyle`로 `ListBoxItem`에 부착 |
| 메뉴 항목이 검정/클릭 가능해 보임 | `IsEnabled="False"` 누락 | placeholder는 비활성으로 둬 "껍데기"임을 명확히 |
| 한글 메뉴 글자 깨짐 | 파일 인코딩이 UTF-8 아님 | `.xaml`을 UTF-8로 저장 |

---

## 다음 단계

- **런북 06**: 이 메뉴의 `명령 실행`에 동작을 연결한다. `Features/SendCommand/` 모듈을 신설하고
  (방법 A/B/C 중 택1, [06 런북](./06_wpf_send_command_to_powershell.md) 참조), `PlacementTarget`으로
  우클릭한 세션의 PID를 집어 커맨드를 주입한다.
- **런북 07**: `종료` 항목에 대상 프로세스 Kill 동작을 연결한다.
