# Runbook 06b — 임베드 터미널: 명령 주입 + 출력 가로채기 (블루프린트)

> **상태**: 큰 틀(블루프린트)만. 상세 구현 단계는 추후 확장.
> **방식**: Runbook 06과 동일 — **직접 구현·검증(PoC) 후 검증된 절차를 런북으로 역산출**한다
> ([feedback-runbook-implement-first] 워크플로). 본 문서는 그 확장의 골격이다.

---

## 목표

Runbook 06에서 만든 임베드 터미널(앱 안 pwsh, 사람 타이핑 가능)에 두 가지를 더한다.

1. **앱 → 터미널 명령 주입**: 사람이 타이핑하지 않아도 앱이 프로그램적으로 명령을 보낸다.
2. **터미널 출력 가로채기**: 터미널 출력을 앱이 읽어 로깅·파싱·자동반응한다.

사람 입력과 앱 주입이 **한 세션에서 공존**하고, 출력은 화면 렌더와 앱 수집이 **동시에** 이뤄진다.

---

## 전제 (사전 조건)

- [ ] Runbook 06 완료 — 앱 내 임베드 pwsh 렌더 + 사람 타이핑 GO 확인
- [ ] `Features/EmbeddedTerminal/`(Views/TerminalView, ViewModels/TerminalViewModel) 존재
- [ ] `EasyWindowsTerminalControl` 동작 확인됨(native conpty.dll·OpenConsole.exe 출력 복사 포함)

---

## 개념 (큰 틀)

```
              ┌─────────── 사람 타이핑 ───────────┐
              ▼                                   │
앱(VM) ─주입─▶ EasyTerminalControl.TermPTY ─▶ ConPTY ─▶ powershell
                       │                                   │
                       └◀── 출력 가로채기(Intercept) ◀──────┘
                            ├─ 화면 렌더(기존)
                            └─ 앱 수집(로깅/파싱)  ← 06b 신규
```

### 사용할 API (06 README에서 확인된 표면)

| 용도 | API |
|------|-----|
| 명령 주입(텍스트) | `TermPTY.WriteToTerm(ReadOnlySpan<char>)` |
| 명령 주입(바이너리) | `TermPTY.WriteToTermBinary(ReadOnlySpan<byte>)` |
| 출력 가로채기 | `TermPTY.InterceptOutputToUITerminal` (델리게이트 `void(ref Span<char>)`) |
| 입력 가로채기 | `TermPTY.InterceptInputToTermApp` (동일 시그니처) |
| 출력 로깅·조회 | `LogConPTYOutput`(bool) + `ConPTYTerm.GetConsoleText()` |
| delimiter 기반 수집 | `ReadDelimitedTermPTY` (특정 구분자까지의 출력만 노출) |

> **주의(README Limitations)**: 출력 가로채기로 ANSI/VT 시퀀스를 변형하면 복잡 프로그램에서
> 커서 위치 등이 어긋날 수 있다. 변형보다 **관찰(로깅/파싱)** 위주로 시작한다.

---

## 아키텍처 통합 (Feature × Layer)

```
Features/EmbeddedTerminal/
├── Interfaces/   ITerminalSession.cs        # 신규 — 주입/출력 계약 (항상 분리 규칙)
├── Services/     (TermPTY 래핑 구현)         # 신규 — 계약 구현
├── ViewModels/   TerminalViewModel.cs        # 06의 빈 앵커 → 주입 커맨드·출력 핸들 추가
└── Views/        TerminalView.xaml(.cs)      # 컨트롤 ↔ VM/Service 연결
```

- **Interfaces 신설**: 주입·출력 계약(예: `ITerminalSession { void Send(string); event Action<string> OutputReceived; }`)을
  `Interfaces/`에 둔다(인터페이스는 1개여도 항상 분리 — [interfaces.md](../guides/convention/interfaces.md)).
- **ViewModel**: 06의 빈 앵커 `TerminalViewModel`에 `ICommand SendCommand`·수신 출력 노출 추가, 계약을 생성자 주입.
- **Shell**: 06 그대로 — 새 의존성은 EmbeddedTerminal 내부에 갇힘(Feature 격리 유지).

---

## 설계 난점 / 선결 항목 (상세 구현 전 PoC로 확인)

- [ ] **VM → 컨트롤 TermPTY 접근 경로**: `EasyTerminalControl`은 View의 요소다. VM이 그 `TermPTY`(또는
      `ConPTYTerm`)에 어떻게 닿을지 — View 코드비하인드에서 VM/Service에 주입할지, attached behavior로 묶을지.
      MVVM 경계를 지키는 방법을 PoC로 확정.
- [ ] **스레드**: `InterceptOutputToUITerminal` 델리게이트가 어느 스레드에서 호출되는지 → `ObservableCollection`/
      바인딩 갱신 시 UI 스레드 전환 필요 여부.
- [ ] **주입-입력 공존**: `WriteToTerm` 주입이 사람 타이핑과 충돌 없이 같은 입력 경로에 합류하는지.
- [ ] **출력 로깅 방식**: 실시간 `Intercept` vs `LogConPTYOutput`+`GetConsoleText()` 폴링 vs `ReadDelimitedTermPTY`.
- [ ] **airspace**: 출력 로그를 터미널 **위**에 오버레이 불가 → 별도 패널(예: 하단/우측 로그 영역)에 표시.

---

## 큰 틀 단계 (추후 상세화)

1. `Interfaces/ITerminalSession.cs` — 주입/출력 계약 선언
2. `Services/` — `TermPTY` 래핑 구현 (계약 구현)
3. `ViewModels/TerminalViewModel` 확장 — `SendCommand`(ICommand) + 출력 수신 노출
4. `Views/TerminalView` — 컨트롤 인스턴스 ↔ VM/Service 연결 (PoC로 경로 확정)
5. 빌드·실행 검증 — 앱에서 명령 1줄 주입 동작 + 출력 캡처 확인 (사람 타이핑 공존 유지)

---

## 다음 단계

- 본 블루프린트를 **PoC → 단계별 구현 런북**으로 확장 (06과 동일 방식).
- **07**: 임베드 세션 **종료/수명** 관리(`RestartTerm`/`DisconnectConPTYTerm`), 별건으로 Runbook 05
  컨텍스트 메뉴 `종료`(외부 프로세스 Kill) 연결.

---

## 참고

- [Runbook 06 — 임베드 터미널 (PoC + 최소 임베드)](./06_wpf_send_command_to_powershell.md)
- [interfaces.md — 계약 전용 레이어](../guides/convention/interfaces.md) ·
  [services.md](../guides/convention/services.md) · [viewmodels.md](../guides/convention/viewmodels.md)
- [EasyWindowsTerminalControl (GitHub)](https://github.com/mitchcapper/EasyWindowsTerminalControl) — TermPTY / Intercept API
- [Creating a Pseudoconsole session — MS Learn](https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session)
