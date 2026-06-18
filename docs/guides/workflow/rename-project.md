# 프로젝트 리네임 워크플로우

> 실제 수행 기록 겸 재사용 절차. 본 프로젝트는 `HelloWorldWpf → ControlTowerWin` 리네임을 이 절차로 완료했다.

## 개요

WPF 프로젝트의 이름(어셈블리·네임스페이스·폴더)을 일괄 변경하는 절차.
WPF는 `x:Class`(XAML)와 `namespace`(C#)가 짝을 이루므로, 한쪽만 바꾸면 빌드가 깨진다.
**파일 이동 → 내용 치환 → 캐시 정리 → 빌드 검증** 순서를 지킨다.

---

## Step 1. 영향 범위 스캔

```bash
# 구 이름이 박힌 모든 지점 확인 (식별자 + 파일명)
grep -rn "HelloWorldWpf" src/
find src -iname "*HelloWorld*"
```

확인 대상: `*.csproj`, `*.slnx`, `App.xaml(.cs)`, `MainWindow.xaml(.cs)`, `AssemblyInfo.cs`,
게시 프로필(`*.pubxml`), 자동생성(`.vs/`, `*.user`, `obj/`, `bin/`).

## Step 2. 폴더·파일 이동 (git mv)

```bash
git mv src/HelloWorldWpf src/ControlTowerWin
git mv src/ControlTowerWin/HelloWorldWpf.csproj src/ControlTowerWin/ControlTowerWin.csproj
git mv src/ControlTowerWin/HelloWorldWpf.slnx   src/ControlTowerWin/ControlTowerWin.slnx
```
> `git mv`로 디렉토리를 옮기면 추적 파일 이동이 스테이징되고, 미추적 파일(.vs 등)도 함께 이동된다.

## Step 3. 파일 내용 치환

| 파일 | 변경 |
|------|------|
| `ControlTowerWin.slnx` | `Path="HelloWorldWpf.csproj"` → `Path="ControlTowerWin.csproj"` |
| `App.xaml` | `x:Class="HelloWorldWpf.App"`, `clr-namespace:HelloWorldWpf` → `ControlTowerWin` |
| `App.xaml.cs` | `namespace HelloWorldWpf` → `ControlTowerWin` |
| `MainWindow.xaml` | `x:Class="HelloWorldWpf.MainWindow"` → `ControlTowerWin.MainWindow` |
| `MainWindow.xaml.cs` | `namespace HelloWorldWpf` → `ControlTowerWin` |
| (선택) `MainWindow.xaml` | `Title`/제목 텍스트 `"Hello World"` → `"Control Tower"` |

> `*.csproj`에 `AssemblyName`/`RootNamespace`가 없으면 파일명 기반으로 자동 적용되어 별도 수정 불필요.

## Step 4. 캐시 정리

```bash
cd src/ControlTowerWin
mv HelloWorldWpf.csproj.user ControlTowerWin.csproj.user   # .user는 csproj 이름과 일치해야 로드됨
rm -rf obj bin .vs                                          # 구 어셈블리명 잔재 제거
```

## Step 5. 빌드 검증

```bash
dotnet build ControlTowerWin.csproj
# 기대: 경고 0 / 오류 0, 산출물 bin/.../ControlTowerWin.dll, ControlTowerWin.exe
```

---

## 주의사항

1. **x:Class ↔ namespace 짝**: XAML과 코드비하인드는 반드시 동시에 바꾼다. 불일치는 빌드에서 잡힌다.
2. **VS 닫고 진행**: Visual Studio가 열려 있으면 `.vs/`·`bin/` 파일 잠금으로 이동·삭제가 실패할 수 있다.
3. **이력 문서 보존**: 과거 런북(예: `01_wpf_hello_world.md`)의 구 이름 언급은 기록이므로 수정하지 않는다.
4. **게시 프로필 확인**: `*.pubxml`이 어셈블리명을 직접 참조하면 갱신한다(본 프로젝트는 미참조라 안전했음).
