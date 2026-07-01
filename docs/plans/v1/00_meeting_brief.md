# Control Tower v1 — 기획 입력 브리프 (역설계 회의 종합)

> 본 문서는 기존 산출물(Notion 메모 · `docs/guides` · `docs/runbooks` · 현재 소스코드)을
> 역설계하여, skill_plan(기획팀) 발동 **전** 진행한 심도 회의(3라운드)의 결과 종합이다.
> skill_plan의 요구분석·기술·DB·로드맵 에이전트의 **입력값**으로 사용한다.

---

## 0. 한 줄 정의

**Control Tower** — 로컬에서 여러 Claude Code/PowerShell 세션을 **앱이 직접 소유(ConPTY 임베드)** 하여,
의도별 **세션 프로파일**로 띄우고 · **IPC 채널**로 묶어 협업시키며 · **토큰·프롬프트 자산**까지
한 곳에서 관제하는 **단일 파워유저용 AI 세션 관제탑**.

- 플랫폼: WPF (.NET 10), Windows 데스크톱
- 구조: Feature × Layer 하이브리드(NestJS 모듈 스타일) — 기존 `Shell/Features/Shared` 계승
- 배포: ClickOnce (runbook 02 계승)

---

## 1. 제품 정체성 & 핵심 가치

Notion 요구사항이 그리는 5개 역량 클러스터:

| # | 클러스터 | 핵심 |
|---|---|---|
| ① | 터미널 오케스트레이션 | 의도별(control / sangmin+noter / Proj_N) 세션 생성·임베드·탭·pane·커맨드 주입 |
| ② | 세션 협업(IPC) | 채널 오픈 → 세션 참여 → IPC 미보유 세션에 skill 주입 |
| ③ | 프롬프트 자산 관리 | 앱 내에서 rules/skills/agents 편집 |
| ④ | 관측 | 토큰 사용량 확인 |
| ⑤ | 멀티 디바이스 | 원격 제어 (**v2**, v1 제외) |

**1차 정체성 = AI 세션 오케스트레이션.** 터미널 임베드는 그 가치를 실현하는 substrate(토대)다.

---

## 2. 결정 로그 (회의 3라운드)

| # | 결정 항목 | 확정 | 근거 |
|---|---|---|---|
| 1 | 제품 정체성 | AI 세션 오케스트레이션 우선 | 제품명·요구 서두의 취지 |
| 2 | v1 범위 | 로컬 전체(ConPTY 임베드 포함), 멀티디바이스만 v2 | — |
| 3 | IPC 관계 | 기존 `skill_ipc_control`의 GUI 프론트엔드 | 채널·relay·스크립트 재사용 |
| 4 | 빌드 순서 | **ConPTY가 0순위 기반** | 오케스트레이션의 모든 능력이 "앱의 세션 I/O 소유"에 의존 |
| 5 | VT 범위 | 단계적 ①기본(색·커서·스크롤백) → ②alt-screen/TUI | 리스크 분산 |
| 6 | 구현 전략 | VT 파서 = 라이브러리, 렌더러 = 자체(WPF) | 시퀀스 파싱 버그 리스크 제거 |
| 7 | 세션 소유 | 앱-소유 ConPTY 세션만, 외부 감지 제거 | ConPTY는 외부 프로세스 attach 불가 |
| 8 | 컨테이너 UI | 단일 pane → 탭 (pane 분할 후속) | 마일스톤 단계화 |
| 9 | 세션 도메인 | 명시적 세션 프로파일 객체 | 오케스트레이션-우선의 핵심 도메인 |
| 10 | IPC 주입 | 트리거 프롬프트 전송(입력 파이프 재사용) | 이 앱이 세션을 시작시키는 방식 그대로 |
| 11 | 토큰 소스 | 세션 jsonl 트랜스크립트 파싱 | Claude Code 공식 기록, 정확·안정 |

### 핵심 의존 함의 (ConPTY 렌더 단계 ↔ 오케스트레이션)

```
IPC 스킬 주입 / 커맨드 전송  ── 입력 파이프만 필요 ──▶  렌더 ①단계에서도 동작 (주입 경로 조기 확보)
claude 화면 관찰 / TUI 관제  ── alt-screen 필요 ────▶  렌더 ②단계 완료 후 (관찰 품질만 대기)
```

claude는 alt-screen TUI라 화면에 제대로 뜨는 건 렌더 ②단계 후지만, **주입/제어 경로는 입력
파이프만으로 ①단계에서 확보**된다. 이 분리 덕에 레이어 2가 렌더러 완성에 볼모잡히지 않는다.

---

## 3. 로드맵 (레이어 = 의존 순서)

```
레이어 0 ★ ConPTY 임베드      앱-소유 pwsh 세션 · VT파서(lib)+셀렌더러(자체) · ①기본→②TUI · 단일 pane
레이어 1   세션 소유·제어      세션 프로파일 · 커맨드/트리거 주입 · claude 실행 · 다중세션 탭
레이어 2   IPC 오케스트레이션  skill_ipc_control 프론트엔드(채널파일 watch + relay/.cmd) · 트리거로 채널 참여
레이어 3   관측·자산          토큰(jsonl 파싱) · 프롬프트(~/.claude/{rules,skills,agents} 편집)
─ v2: 멀티디바이스 원격제어 ─
```

---

## 4. 도메인 모델 스케치

```
SessionProfile { 의도태그, 초기명령, 작업디렉토리, 주입스킬[], 채널멤버십[], claude자동실행 }
Session(런타임) { profile, conpty핸들, pid, as(IPC식별자), 상태 }
Channel         { name, relayUrl, members }        ← skill_ipc_control channel과 1:1 매핑
TokenUsage      { session참조, source = jsonl }
```

- **세션 프로파일**이 오케스트레이션-우선의 중심 애그리거트. 저장·재사용 가능해야 한다.
- **Session.as** = IPC 식별자(예: `tower2`, `sangmin-noter`) — OS 프로세스 ↔ IPC 정체성 ↔ 채널 멤버십을 잇는 키.
- 앱의 **Channel** 개념은 `skill_ipc_control`의 channel과 1:1(로 가정, §7 오픈이슈).

---

## 5. IPC 통합 (GUI 프론트엔드 계약)

- 앱은 `skill_ipc_control`(방식 A~F, 파일 + HTTP relay 기반)의 **프론트엔드**다. IPC를 재구현하지 않는다.
- **상태 소스(가정)**: `channels/<ch>/`의 `inbox.log` · `.relay_url` · `.cursor_<as>`를 앱이 직접 read/watch,
  전송·URL설정은 기존 `send.cmd` / `set_url.cmd` / relay HTTP 재사용.
- **IPC 주입 메커니즘(확정)**: 대상 세션 터미널(ConPTY 입력 파이프)에 "IPC 통신 시작 …" **트리거 프롬프트를 전송** →
  세션이 스스로 `skill_ipc_control`을 발동. (로컬은 `~/.claude` 공유이므로 스킬 파일 배치 불필요; 멀티디바이스=v2에서 파일 배치 필요)
- 기존 `tools/web_monitor`(채널 시각화 웹앱)와의 관계는 후속 정리(앱이 흡수 vs 병존).

---

## 6. 기존 코드 처리 방침

| 자산 | 처리 |
|---|---|
| `Features/SessionMonitor` (`ProcessTracker` 전역 감지) | **폐기** → 앱-소유 세션 모델로 대체 |
| `Features/TerminalLauncher` (`wt -w -1 nt` 새창) | ConPTY spawn으로 **대체** (외부 wt 열기는 보조 기능으로 잔류 검토) |
| `Shell / Features / Shared` 하이브리드 구조 | **유지** — 신규 기능 모듈로 확장 |
| runbook 05 (send-command A/B/C 탐색) | ConPTY 채택으로 대체됨 — **역사 자료** |
| `Shared/Core` (ViewModelBase, RelayCommand) | 유지 |

---

## 7. 오픈 이슈 (skill_plan/후속 결정)

- 세션 프로파일 **영속화 형태**(JSON 파일 vs SQLite) — DB 모델러
- **VT 파서 라이브러리 선정**(.NET용) — 기술 리서처
- 렌더러 **성능 목표**(대량 출력 스크롤·리페인트) — 기술 스펙
- 앱 "채널" = IPC channel **1:1 매핑 최종 확정**
- 프롬프트 편집 v1 범위(파일 트리 읽기/쓰기까지, 검증·스키마는 후속)

---

## 8. 기본 가정 (이견 시 정정)

- 타깃 = **단일 파워유저(본인)** 가 자기 Claude 세션 함대를 관제 / **로컬 전용·인증 없음**
- 배포 = 기존 **ClickOnce** 계승
- 프롬프트 편집 = `~/.claude/{rules,skills,agents}` **파일 트리 브라우저 + 에디터**(v1 읽기/쓰기)
