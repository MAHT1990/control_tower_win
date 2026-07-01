# Runbook 07 — 세션 종료 / 수명 관리 (블루프린트)

> **상태**: 큰 틀(블루프린트)만. 상세 구현 단계는 추후 확장.
> **방식**: Runbook 06과 동일 — **직접 구현·검증(PoC) 후 검증된 절차를 런북으로 역산출**한다
> ([feedback-runbook-implement-first] 워크플로). 본 문서는 그 확장의 골격이다.

---

## 목표

"세션 종료"는 이 프로젝트에서 **두 가지 다른 대상**을 가리킨다. 07은 둘 다 다룬다.

- **(A) 임베드 터미널 세션 수명** — 앱이 직접 spawn한 EmbeddedTerminal(ConPTY) 세션의 종료·재시작·정리.
- **(B) 외부 세션 종료** — SessionMonitor 목록의 외부 `pwsh` 프로세스를 우클릭 `종료`로 Kill.
  Runbook 05에서 만든 컨텍스트 메뉴의 `종료`(현재 `IsEnabled="False"`) placeholder를 **활성화**한다.

> **경계 재확인**: A는 *앱이 부모인* 임베드 세션(입출력 제어 가능), B는 *남의 프로세스*(Kill만 가능).
> 둘은 별개 Feature이며 서로 직접 참조하지 않는다([06 개념](./06_wpf_send_command_to_powershell.md) 계승).

---

## 전제 (사전 조건)

- [ ] Runbook 05 완료 — `SessionListView`에 우클릭 컨텍스트 메뉴(`명령 실행`/`종료` placeholder) 존재
- [ ] Runbook 06 완료 — `Features/EmbeddedTerminal/` 임베드 터미널 동작
- [ ] SessionMonitor의 `ProcessTracker`가 외부 pwsh를 추적·이벤트 통지 중

---

## 개념 (큰 틀)

### (A) 임베드 세션 수명

```
창 닫힘 / Close 커맨드
   │
   ▼
EmbeddedTerminal: TermPTY/ConPTYTerm 정리 → 자식(OpenConsole/pwsh) 종료 → 좀비 방지
   └ RestartTerm() : 깨끗한 새 term으로 재시작 (StartupCommandLine 재적용)
   └ DisconnectConPTYTerm() : 프론트엔드에서 분리(다른 term 연결 대비)
```

### (B) 외부 세션 Kill (05 컨텍스트 메뉴 활성화)

```
세션 항목 우클릭 → [종료] 클릭
   │  (ContextMenu는 별도 비주얼 트리 → PlacementTarget으로 항목 DataContext/PID 획득)
   ▼
SessionListViewModel.KillCommand(pid)
   ▼
ProcessTracker.Kill(pid)  →  Process.Kill()
   ▼
기존 Exited 이벤트 + 폴링 이중 안전망이 목록에서 제거 (Runbook 03/04 메커니즘 재사용)
```

- **PlacementTarget**: Runbook 05에서 "동작 연결 시 PlacementTarget으로 PID를 집어야 한다"고 예고한 미해결 지점.
  `ContextMenu`는 메인 비주얼 트리 밖이라, 우클릭한 `ListBoxItem`의 `DataContext`(= `TerminalInfo`)를
  `PlacementTarget`으로 끌어와 PID를 얻는다.
- **권한**: 외부 프로세스 Kill은 권한 부족 시 예외(`Win32Exception`/Access Denied) 가능 → try-catch.

---

## 아키텍처 통합 (Feature × Layer)

### (B) SessionMonitor — 외부 Kill
```
Features/SessionMonitor/
├── Services/     ProcessTracker.cs        # Kill(int pid) 추가 (추적 Process를 Kill, 예외 흡수)
├── ViewModels/   SessionListViewModel.cs   # KillCommand(ICommand) 노출
└── Views/        SessionListView.xaml      # ContextMenu의 <MenuItem Header="종료"> → Command 바인딩 + IsEnabled 해제
```

### (A) EmbeddedTerminal — 세션 수명
```
Features/EmbeddedTerminal/
├── Interfaces/   ITerminalSession.cs       # (06b에서 도입) Close/Restart 계약 추가
├── ViewModels/   TerminalViewModel.cs       # CloseCommand / RestartCommand 노출
└── Views/        TerminalView.xaml(.cs)     # 창 종료(Unloaded/Closing) 시 정리 훅
```

> Feature 격리: SessionMonitor의 Kill과 EmbeddedTerminal의 수명은 서로 독립. 공유 없음.

---

## 설계 난점 / 선결 항목 (상세 구현 전 PoC로 확인)

- [ ] **PlacementTarget → PID**: ContextMenu의 `MenuItem`에서 우클릭 대상 `ListBoxItem.DataContext`(PID)를
      `CommandParameter`로 전달하는 바인딩 식 확정 (`{Binding PlacementTarget.DataContext, RelativeSource=...}`).
- [ ] **권한 부족 Kill**: 보호된/관리자 프로세스 Kill 시 예외 처리 + 사용자 피드백.
- [ ] **Kill 후 목록 정리**: 기존 `Process.Exited` + 폴링 `Except` 이중 안전망이 Kill에도 정상 동작하는지.
- [ ] **확인 다이얼로그**: 실수 종료 방지용 확인 프롬프트 필요 여부(`Shared/Core/Interfaces/IDialogService` 후보).
- [ ] **임베드 세션 좀비 방지**: 창 닫힘 시 ConPTY 자식(OpenConsole/pwsh)이 확실히 종료되는지(Dispose 경로).
- [ ] **재시작 UX**: `RestartTerm` 시 화면/스크롤백 초기화 동작 확인.

---

## 큰 틀 단계 (추후 상세화)

1. **(B 먼저 — 05 연속성)** `ProcessTracker.Kill(pid)` 구현(예외 흡수)
2. `SessionListViewModel.KillCommand` 노출
3. `SessionListView.xaml`: `종료` MenuItem → `Command` 바인딩 + `IsEnabled` 해제 + `PlacementTarget`으로 PID 전달
4. 검증: 항목 우클릭 → 종료 → 해당 세션이 목록에서 사라지는지(이중 안전망)
5. **(A)** `TerminalViewModel`에 `CloseCommand`/`RestartCommand` + `TerminalView` 종료 정리 훅
6. 검증: 임베드 터미널 종료/재시작 + 창 닫힘 시 자식 프로세스 정리 확인

---

## 다음 단계

- 본 블루프린트를 **PoC → 단계별 구현 런북**으로 확장 (06과 동일 방식).
- 이후: 다중 터미널 탭, 출력 로그 패널, 세션 영속화 등 확장 기능 검토.

---

## 참고

- [Runbook 05 — 우클릭 컨텍스트 메뉴(껍데기)](./05_wpf_session_context_menu.md) — `종료` placeholder·PlacementTarget 예고
- [Runbook 06 — 임베드 터미널](./06_wpf_send_command_to_powershell.md) ·
  [Runbook 06b — 명령 주입·출력 가로채기](./06b_wpf_command_injection_output_capture.md)
- [services.md — ProcessTracker / GC 방지 강한 참조](../guides/convention/services.md) ·
  [viewmodels.md](../guides/convention/viewmodels.md) · [interfaces.md](../guides/convention/interfaces.md)
- [EasyWindowsTerminalControl (GitHub)](https://github.com/mitchcapper/EasyWindowsTerminalControl) — RestartTerm / DisconnectConPTYTerm
