# Runbook 06 — 세션에 명령 전송: ConPTY 임베드 터미널 (블루프린트)

> **상태**: 큰 틀(블루프린트)만. 상세 구현 단계는 추후 확장.
> **배경**: 방법 A(stdin)·B(WriteConsoleInput) 실험 후 **ConPTY 채택**. 방법 비교·실험 결과·한계 전문은
> Notion **"MEMO: Control-Tower"** 하단 정리 참조.

---

## 목표

앱 **안에 임베드된 실제 터미널**에서 PowerShell(나아가 `claude`·`python`)을 실행한다.

- **사람 직접 타이핑** + 앱의 **프로그램적 명령 주입**이 한 세션에서 공존
- 터미널 **출력을 앱이 가로채** 로깅·파싱·자동반응 가능
- `claude`·`python` 같은 **인터랙티브/TUI** 프로그램이 정상 렌더링

---

## 왜 ConPTY인가 (요약)

- **"파이프 ≠ 터미널(TTY)"**. 인터랙티브/TUI는 `isatty()`로 진짜 PTY를 요구하고, raw 모드·키 이벤트·전체화면을 콘솔에서만 한다.
- 방법 A(stdin): **사람 입력 차단**, TUI 불가. 방법 B(WriteConsoleInput): **출력 못 읽음** + conhost 강제 필요.
- **ConPTY만** "화면 + 사람입력 + 앱입력 + claude/python + 출력읽기"를 모두 충족.

---

## 채택 방식: EasyWindowsTerminalControl (라이브러리)

직접 ConPTY 플러밍을 짜면 claude 같은 풀스크린 TUI 렌더링이 별도 대공사다. 그래서 **공식 Windows Terminal
백엔드를 렌더러로 쓰는 NuGet 컨트롤**을 임베드한다.

- 패키지: **`EasyWindowsTerminalControl`** (WPF). (WinUI3용은 `EasyWindowsTerminalControl.WinUI`, alpha)
- 내부: `conpty.dll`에 직접 후킹(TermPTY). **beta 패키지 의존 → 저수준 API 변동 가능성 유의.**

### 핵심 API (확인된 표면)

| 용도 | API |
|------|-----|
| 컨트롤 배치 | `<term:EasyTerminalControl StartupCommandLine="pwsh.exe"/>` |
| 실행할 콘솔 앱 | `StartupCommandLine` 속성 (pwsh/cmd/claude 등) |
| **앱 입력 주입** | `TermPTY.WriteToTerm(ReadOnlySpan<char>)` / `WriteToTermBinary(ReadOnlySpan<byte>)` |
| **출력 가로채기** | `TermPTY.InterceptOutputToUITerminal`, `InterceptInputToTermApp` (delegate: `void(ref Span<char>)`) |
| 출력 로깅 | `LogConPTYOutput`(bool) + `ConPTYTerm.GetConsoleText()` |

---

## 아키텍처 통합 (큰 틀)

```
Shell/MainWindow
 ├─ (기존) SessionMonitor : 외부 pwsh 프로세스 목록
 └─ (신규) Features/EmbeddedTerminal/
      ├─ Views/        TerminalView.xaml(.cs)   ← EasyTerminalControl 호스팅
      ├─ ViewModels/   TerminalViewModel.cs      ← 입력 주입 커맨드 · 출력 핸들
      └─ Services/     (필요 시) 터미널 세션 수명 관리
```

흐름:
```
사람 ─타이핑─▶ EasyTerminalControl ─▶ ConPTY ─▶ pwsh
앱   ─VM 커맨드─▶ TermPTY.WriteToTerm ─▶ ConPTY ─▶ pwsh   (둘이 같은 입력 경로에서 공존)
pwsh ─출력─▶ ConPTY ─▶ InterceptOutputToUITerminal ─▶ VM(로깅/파싱) + 화면 렌더
```

- **Shell**이 `TerminalView`를 영역(ContentControl/패널/탭)에 배치 — 기존 Feature×Layer 하이브리드 규약 준수.
- **Feature 격리**: SessionMonitor는 EmbeddedTerminal을 직접 참조하지 않음. 조합은 Shell이.
- **인터페이스 분리**: 주입/출력 계약은 `Interfaces/`에([interfaces.md] 규약), 구현은 `Services/`.

---

## 선결·검증 항목 (상세 구현 전 PoC로 확인)

- [ ] **`EasyWindowsTerminalControl`의 .NET 10(`net10.0-windows`) WPF 호환성** — README에 명시 없음 → 실측 필요
- [ ] 의존 native(conpty.dll / WT 백엔드) 배포·RID(win-x64) 요건
- [ ] 라이선스 확인 (배포 가능 여부)
- [ ] PoC: `StartupCommandLine="pwsh.exe"`로 임베드 → 사람 타이핑 / `WriteToTerm` 주입 / `claude` 실행 / 출력 가로채기 동작 확인

---

## 개념 런북(별도 예정): ConPTY 내부 원리

라이브러리로 "작동"을 확보한 뒤, 저수준 개념을 별도 런북으로 정리한다.

- `CreatePseudoConsole` + 입력/출력 익명 파이프 + `STARTUPINFOEX`(`PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`)로 자식에 PTY 부착
- 출력 파이프 = VT100 스트림, 입력 파이프 = 키 입력
- 내부적으로 **headless conhost**가 ConPTY 뒤에서 동작(창 없음) → 화면 렌더는 앱(여기선 WT 백엔드)이 담당
- 참고 소스: MS `terminal` 레포 `GUIConsole.ConPTY` 샘플, `EasyWindowsTerminalControl` 소스(둘 다 오픈소스)

---

## 다음 단계

- 오후 **신규 기획** 후, 이 블루프린트를 **상세 구현 런북**으로 확장 (PoC → 단계별 구현).
- **07**: 컨텍스트 메뉴 '종료' 등 세션 제어 기능.

---

## 참고

- [Creating a Pseudoconsole session — MS Learn](https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session)
- [microsoft/terminal — ConPTY GUIConsole 샘플](https://github.com/microsoft/terminal/tree/main/samples/ConPTY/GUIConsole)
- [EasyWindowsTerminalControl (GitHub)](https://github.com/mitchcapper/EasyWindowsTerminalControl) · [NuGet](https://www.nuget.org/packages/easywindowsterminalcontrol/)
