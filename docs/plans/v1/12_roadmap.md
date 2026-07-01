# 12. 실행 로드맵·일정 (Execution Roadmap)

> 담당: plan_roadmap_planner · 깊이: deep · 5 단계(P0·L0·L1·L2·L3) / 4 레이어 + 횡단 X · FR 전수 매핑 43/43
> 본 문서는 FR 43을 레이어/단계로 전수 배치하고, RISK-001~004를 선행 Phase 0(압축 PoC)로 차단한 뒤, Gate 기반 상대 일정으로 실행 순서를 제시한다.

---

## 0. 개요

### 0-1. 목적·범위

본 문서는 `04`(FR 43/NFR 22·후속숙제 ①~⑧)·`05`(FN 53)·`09`(ENT 18·JSON-first)·`10`(RISK 10·압축 PoC·아키텍처)를 입력으로, Control Tower v1의 **실행 로드맵**을 구성한다. 모델은 **레이어 적층 + 선행 PoC**다.

- **정의하는 것**: (a) 레이어/단계 모델과 적층 순서 근거(데이터·기능·리스크 선행성) / (b) **압축 PoC(Phase 0)** 실험 설계와 Gate0 / (c) **FR 43 전수 배치**(미배치 0) / (d) 단계별 산출·진입 Gate·완료조건 / (e) 의존성·병렬 트랙·기존 코드 마이그레이션 / (f) 후속숙제 ①~⑧ 해소 시점.
- **정의하지 않는 것(경계)**: 요구 자체=04 / 기능 분해=05 / 데이터 스키마=09 / 스택·PoC 성공 기준의 기술 근거=10 / 테스트 케이스·QA 게이트 상세=11.
- **불변식(최우선)**: 모든 FR이 ≥1 레이어/단계에 배치된다 — **미배치 FR = 0**(§1-2·§3 검증). 본 문서는 **신규 레지스트리 ID를 발번하지 않는다**(FR·FN·ENT·RISK 참조 전용, 고아 참조 0).

### 0-2. 로드맵 모델·ID 체계

- **적층 모델**: 아래에서 위로 **P0(압축 PoC) → L0(터미널 substrate) → L1(세션 소유·제어) → L2(IPC 오케스트레이션) → L3(관측·자산)**, 횡단 **X(SEC·SYS)** 가 전 레이어를 관통.
- **레이어 라벨의 이중성 해소(중요)**: 본 문서의 `L0~L3`은 **브리프(00 §3)의 의존 순서 레이어 번호**를 그대로 계승한다. 이는 규약(plan_id_system §L0=압축PoC)의 **일반 번호와 의미가 다르다**. 충돌을 피하기 위해:
  - 규약의 "L0 압축 PoC/Walking Skeleton"은 본 문서에서 **`Phase 0`(P0)** 로 표기(선행 단계).
  - 규약의 "L1 핵 MVP"는 본 프로젝트에서 **`L0+L1` 완성 = MVP 출시선(Gate2)** 에 해당한다(터미널 substrate + 세션 소유·제어가 갖춰져야 "AI 세션 오케스트레이션" 정체성이 성립, 결정 로그 #1·#4).
- **라벨 성격**: `P0`·`L0~L3`·`Gate0~4`·`M-*`(마일스톤)·`S0~Sn`(상대 스프린트)는 **본 문서 내부 라벨**이며 **레지스트리 네임스페이스가 아니다(발번하지 않는다)**. FR-###·FN-{CAT}-##·ENT-###·RISK-###는 상위 문서의 frozen ID를 **참조만** 한다.

### 0-3. 표기 규칙 (Gate·상대 일정)

- **Gate(진입조건)**: 다음 단계로 진행하기 위해 충족해야 하는 선행 산출·검증 기준. 일정은 **캘린더 날짜가 아니라 Gate 충족**으로 진행한다(§5).
- **상대 일정**: `S0, S1, …`(상대 스프린트/구간). Gate에 앵커된 상대 창이며 **재배열 가능·달력 고정 아님**.
- **우선순위**: Must/Should/Could(04 §0-3 승계). Must=해당 레이어 핵심 산출, Should/Could=레이어 후반 또는 후속 마일스톤.
- **완료조건**: 관찰 가능한 통과 기준(정량치는 10 §7 PoC 실측·11 테스트 게이트 연계). `[11]`=테스트 전략 문서 게이트 참조.
- **RISK 대응**: 각 단계에 소급 RISK-###를 명기. ★최상 4건(001·002·003·004)은 **P0에서 선제**.
- **마이그레이션**: `MIG-#`=기존 코드 전환 단계(ProcessTracker·TerminalLauncher 대체).

---

## 1. 단계·레이어 한눈에

### 1-1. 레이어 개념도 (적층 + 횡단 + Gate)

```
  +===============================================================+
  | X  Cross-cutting : SEC(safety) . SYS(shell/deploy/settings)   |   횡단 - 전 레이어 관통
  +===============================================================+
  | L3  Observability + Asset      (OBS . AST)     [관측 . 자산]  |   Could/Should 확장
  +---------------------------------------------------------------+
  | L2  IPC Orchestration          (IPC)           [채널 협업]    |   전체 기능 적층
  +---------------------------------------------------------------+
  | L1  Session Ownership/Control  (SES . PRF)     [MVP 완성]     | <== MVP 출시선(Gate2)
  +---------------------------------------------------------------+
  | L0  ConPTY Terminal Substrate  (TRM)           [렌더(1) 기반] |   substrate
  +---------------------------------------------------------------+
  | P0  Compressed PoC / Walking Skeleton   (RISK-001~004 선제)   |   출시 비대상
  +===============================================================+
   P0 --[Gate0]--> L0 --[Gate1]--> L1 --[Gate2:MVP]--> L2 --[Gate3]--> L3 --[Gate4]-->

   병렬 트랙(주입/제어는 렌더(1)에서 확보 -> 렌더(2)에 볼모 아님, 브리프 §2):
     [렌더(2)/alt-screen TUI]  ....(L1 이후 착수, L2와 병렬, 후속숙제 (7)).... M-R2
     [pane 분할 FR-008]        ....(MVP 이후, Could).......................... M-PANE
     [상태 복원 FR-043]        ....(L3 이후, Could)............................ M-RESTORE
```
> 아래에서 위로 적층. **L0+L1 = 핵 MVP**(Gate2 출시선). L2·L3는 Gate 통과 시 적층. SEC/SYS는 특정 레이어가 아니라 **전 레이어에 상시 편입**되며 핵심 정책(FR-038/039/040)은 L0/L1 baseline에서 이미 강제된다.

### 1-2. FR↔레이어 전수 매핑 (43/43 · 미배치 0)

| FR | 제목(축약) | 우선 | 레이어 | 배치 마일스톤 | 대응 RISK |
|---|---|:--:|:--:|---|---|
| FR-001 | ConPTY spawn | Must | L0 | M1-L0 (P0 실증) | RISK-001 |
| FR-002 | I/O 파이프 바인딩 | Must | L0 | M1-L0 (P0 실증) | RISK-001·004 |
| FR-003 | VT 파싱(lib) | Must | L0 | M1-L0 (P0 실증) | RISK-003 |
| FR-004 | 셀 렌더러 [렌더①] | Must | L0 | M1-L0 (P0 실증) | RISK-002 |
| FR-005 | alt-screen/TUI [렌더②] | Should | L0 | **M-R2**(병렬, L2와) | RISK-003·005 |
| FR-006 | 탭 컨테이너 | Must | L0 | M1-L0 | RISK-002 |
| FR-007 | 리사이즈·스크롤백 | Should | L0 | M1-L0(후반) | RISK-002 |
| FR-008 | pane 분할 | Could | L0 | **M-PANE**(후속) | — |
| FR-009 | 키보드 입력 포커스 | Must | L0 | M1-L0 (P0 실증) | RISK-002 |
| FR-010 | 선택·클립보드 복사 | Should | L0 | M1-L0(후반) | — |
| FR-011 | 프로파일 기반 기동 | Must | L1 | M2-L1(MVP) | RISK-006 |
| FR-012 | cwd·초기명령 적용 | Must | L1 | M2-L1(MVP) | — |
| FR-013 | claude 자동 실행 | Must | L1 | M2-L1(MVP) | — |
| FR-014 | 커맨드 주입 | Must | L1 | M2-L1(MVP, P0 실증) | RISK-001 |
| FR-015 | 종료/강제 종료 | Must | L1 | M2-L1(MVP) | RISK-001·006 |
| FR-016 | 런타임 상태 추적 | Must | L1 | M2-L1(MVP) | RISK-006 |
| FR-017 | 다중 세션 목록·전환 | Must | L1 | M2-L1(MVP, MIG-2) | RISK-006 |
| FR-018 | 세션 재시작 | Should | L1 | M2-L1(후반) | RISK-006 |
| FR-019 | 프로파일 생성·편집 | Must | L1 | M2-L1(MVP) | RISK-009 |
| FR-020 | 프로파일 영속·재사용 | Must | L1 | M2-L1(MVP) | RISK-009 |
| FR-021 | 프로파일 복제·삭제 | Should | L1 | M2-L1(후반) | — |
| FR-022 | 의도별 프리셋 | Should | L1 | M2-L1(후반) | — |
| FR-023 | Session.as 정의 | Must | L1 | M2-L1(MVP) | RISK-008 |
| FR-024 | 채널 watch/read | Must | L2 | M3-L2 | RISK-008 |
| FR-025 | 메시지 송신(send.cmd) | Must | L2 | M3-L2 | RISK-008 |
| FR-026 | 세션-채널 멤버십 | Must | L2 | M3-L2 | RISK-008 |
| FR-027 | IPC 스킬 트리거 주입 | Must | L2 | M3-L2 | RISK-001 |
| FR-028 | 채널 대화 뷰 | Should | L2 | M3-L2(후반) | RISK-008 |
| FR-029 | 앱 채널↔IPC 1:1 매핑 | Must | L2 | M3-L2 (요구확정 ④) | RISK-008 |
| FR-030 | jsonl 탐지·매핑 | Must | L3 | M4-L3 | RISK-007 |
| FR-031 | 토큰 파싱·집계 | Must | L3 | M4-L3 | RISK-007 |
| FR-032 | 세션별 토큰 표시 | Should | L3 | M4-L3(후반) | — |
| FR-033 | 자산 트리 브라우저 | Must | L3 | M4-L3 (요구확정 ⑤) | — |
| FR-034 | 자산 파일 읽기 | Must | L3 | M4-L3 | — |
| FR-035 | 자산 파일 쓰기·저장 | Must | L3 | M4-L3 | RISK-009 |
| FR-036 | 자산 CRUD | Should | L3 | M4-L3(후반) | — |
| FR-037 | 위험 커맨드 가드 | Should | X(SEC) | MX(L2 前 활성, ⑧) | — |
| FR-038 | 로컬 전용·포트 0 | Must | X(SEC) | MX(L0 baseline) | — |
| FR-039 | 앱-소유 범위 강제 | Must | X(SEC) | MX(L0/L1 baseline, MIG-2) | RISK-006 |
| FR-040 | 앱 셸 레이아웃 | Must | X(SYS) | MX(L0 baseline) | — |
| FR-041 | ClickOnce 배포 | Must | X(SYS) | MX(MVP 출시@Gate2) | RISK-010 |
| FR-042 | 앱 설정 관리 | Should | X(SYS) | MX(L2/L3) | — |
| FR-043 | 상태 복원 | Could | X(SYS) | **M-RESTORE**(L3 후) | — |

> **미배치 FR = 0 (43/43)** ✅ 불변식 충족. Must 30 전량이 L0/L1(MVP)·L2·필수 횡단에, Should 11·Could 2는 레이어 후반 또는 후속 마일스톤(M-R2·M-PANE·M-RESTORE)에 배치.

### 1-3. 단계·마일스톤 한눈에 (산출·Gate·상대 구간)

| 단계 | 레이어/트랙 | 핵심 산출 | 배치 FR(수) | 진입 Gate | 완료(출구) Gate | 상대 구간 |
|---|---|---|:--:|---|---|:--:|
| **P0** | 압축 PoC | ConPTY→I/O→VtNetCore→최소 셀렌더 vertical-slice | (실증 6+) | — (착수) | **Gate0** | S0 |
| **L0** | 터미널 substrate(TRM) | 임베드 터미널·렌더①·탭·입력·리사이즈 | 8 (+2 후속) | Gate0 | **Gate1** | S1–S2 |
| **L1** | 세션 소유·제어(SES·PRF) | 프로파일 기동·claude·주입·다중세션·영속 | 13 | Gate1 | **Gate2=MVP** | S3–S5 |
| **L2** | IPC 오케스트레이션(IPC) | 채널 watch·송신·멤버십·스킬주입·매핑 | 6 | Gate2 | **Gate3** | S6–S7 |
| **L3** | 관측·자산(OBS·AST) | jsonl 토큰 집계·자산 트리 읽기/쓰기 | 7 | Gate3 | **Gate4** | S8–S9 |
| **X** | 횡단(SEC·SYS) | 정책·셸·배포·설정·진단(상시) | 7 | 상시(레이어 편입) | 각 Gate 부분 충족 | S0–S9 |
| **M-R2** | 렌더② 병렬 | alt-screen/claude TUI 렌더 | 1 (FR-005) | Gate1+MVP 안정 | M-R2 완료 | S6–S8(병렬) |
| **M-PANE** | pane 분할 | 탭 내 분할 | 1 (FR-008) | Gate2(MVP) | — | 후속 |
| **M-RESTORE** | 상태 복원 | 프로파일 재기동 제안 | 1 (FR-043) | Gate4 | — | 후속 |

---

## 2. Phase 0 — 압축 PoC (RISK 집중 · Walking Skeleton)

> 근거: 10 §7-11 압축 PoC. **RISK-001·002·003·004(★최상 4)** 는 C2("모든 오케스트레이션 능력이 앱의 세션 I/O 소유에 의존") 때문에 v1 전체의 **임계경로**다. L0 본착수 전에 **하나의 vertical-slice**로 묶어 최단 경로로 불확실성을 제거한다. **출시 비대상**(throwaway 실험, 종단 한 줄기만 관통).

### 2-1. PoC 검증 가설 (RISK 소급)

| PoC 실험 | 검증 가설 | 소급 RISK | 소급 후속숙제 |
|---|---|---|---|
| E1. ConPTY 소유 왕복 | 앱이 pwsh를 spawn하고 파이프·lifecycle·resize를 자체 소유해도 좀비/데드락/누수 없이 통제 가능하다 | RISK-001·006 | — |
| E2. 입력경로 조기 확보 | 입력 파이프 write만으로 렌더② 없이도 커맨드/특수키 주입이 ≤100ms에 반영된다(브리프 §2) | RISK-001 | — |
| E3. VtNetCore 적합성 | VtNetCore(MIT)가 색·커서·스크롤백·alt-buffer·claude TUI를 어댑터 경계 뒤에서 정확히 커버한다 | RISK-003·005 | **②** 파서 선정 |
| E4. 렌더 성능 | WPF 자체 셀 렌더러(GlyphRun+dirty-diff)가 대량 출력에서 ≥30fps·리페인트 ≤50ms를 지킨다 | RISK-002 | **③** 렌더 성능 목표 |
| E5. I/O 스레딩 안정 | 파이프별 전용 스레드+bounded Channel+Dispatcher가 8세션 동시 폭주에서 데드락 없이 흡수한다 | RISK-004 | — |

### 2-2. 최소 산출 (Walking Skeleton — 종단 관통)

10 §7-11의 단일 왕복을 그대로 실행 단위로 채택:

```
[PoC vertical-slice : ConPTY -> pipe I/O -> VT parse -> minimal cell render]
 1) ConPTY로 pwsh spawn                                   (E1 / RISK-001)   <- TerminalLauncher(wt) 대체 실증 MIG-1
 2) IN-pipe write: 커맨드 + Ctrl+C/방향키 주입            (E2 / NFR-002)    <- 입력경로(렌더 무관, 브리프 §2)
 3) OUT-pipe 전용 스레드 -> bounded Channel -> VtNetCore   (E3·E5 / RISK-003·004)
 4) 최소 GlyphRun 셀 렌더 + dirty-region diff             (E4 / RISK-002)
 5) 스트레스: 10만 라인 + 빠른 스크롤 + claude alt-screen  (E4·E3 / NFR-001, C10)
 6) 8세션 동시 + 동시 주입                                 (E5 / RISK-004·NFR-003)
 7) 100x spawn/kill 반복                                   (E1 / RISK-001 누수/좀비/데드락)
```
- 실증 대상 FR(본 배치는 L0/L1, PoC는 실증만): FR-001·002·003·004·009(L0) + FR-014(L1 입력경로) + FR-005 alt-screen 진입 스트레스(L0/렌더②).
- 실증 대상 FN: FN-TRM-01·03·04·05·12 + FN-SES-04.
- 실증 대상 ENT: ENT-004(session 런타임)·ENT-001(screen_buffer) — 런타임 인메모리만(영속 0).

### 2-3. 성공 판정 (Gate0 — 정량)

| 판정 축 | 기준(통과) | 소급 |
|---|---|---|
| lifecycle 안정 | 100x spawn/kill에서 **좀비 0·핸들 누수 0·데드락 0** | RISK-001·004 |
| 렌더 처리량 | **평균 ≥30fps · 리페인트 p95 ≤50ms · 프레임 드랍/멈춤 0 · 메모리 누수 0** | RISK-002·NFR-001 |
| 주입 지연 | 커맨드/특수키 write→반영 **≤100ms** | NFR-002 |
| 파서 커버리지 | claude alt-screen TUI **시각적 깨짐 0**, 핵심 시퀀스(SGR·CUP·ED/EL·alt-buffer·DECAWM) 정확 | RISK-003·005 |
| 동시성 | 8세션 동시 폭주 후 idle 복귀 시 **CPU ≤10%** | RISK-004·NFR-003 |

- **Gate0 통과 효과**: (a) **후속숙제 ②** 해소(VtNetCore INTEGRATE 확정, 실패 시 XtermSharp engine으로 어댑터만 교체 재측정 — 경계 검증) · (b) **후속숙제 ③** 해소(NFR-001 렌더 성능 목표치를 실측으로 확정) · (c) L0 본착수 승인.
- **Gate0 실패 시**: 파서 교체(어댑터 경계 재검증)·렌더 최적화(coalesce Hz·backpressure·스크롤백 상한) 반복 후 **L0 상대 일정 재산정**(10 §7-11). 임계경로이므로 여기서 막히면 상위 레이어 착수 금지.

---

## 3. Phase별 상세 (레이어 전수 · 진입/완료 Gate · 의존 · 마이그레이션)

### 3-1. L0 — ConPTY 터미널 substrate (TRM) [렌더① 기반]

- **목적**: 앱-소유 임베드 터미널의 세션 소유·I/O·파싱·렌더·탭·입력 기반 구축. 모든 상위 오케스트레이션이 이 위에서 동작(C2). 단독 가치는 제한적(Windows Terminal-lite)이므로 **출시선은 L1(MVP)**.
- **진입 Gate**: **Gate0**(P0 통과 — 파서·렌더·lifecycle 검증 완료).
- **배치 FR(10)**: FR-001·002·003·004·006·009(Must 핵심) · FR-007·010(Should, 후반) · FR-005(렌더②→**M-R2 병렬**) · FR-008(pane→**M-PANE 후속**).
- **핵심 FN**: FN-TRM-01~13.
- **선행 ENT**: ENT-004(session 런타임)·ENT-001(screen_buffer)·ENT-002(scrollback)·ENT-003(terminal_tab) — 전부 [R] 런타임(영속 0).
- **산출물**: ① ConPTY P/Invoke 얇은 래퍼(spawn/resize/close) · ② `ITerminalScreenModel` 파서-렌더 경계 인터페이스(NFR-018) · ③ VtNetCore 어댑터(vendored/fork) · ④ GlyphRun 셀 렌더러(dirty-diff) · ⑤ 탭 컨테이너(단일 pane→탭, C8) · ⑥ 키보드 입력→VT 인코딩 · ⑦ 리사이즈 재래핑·스크롤백 뷰포트 · ⑧ 선택·클립보드 복사.
- **마이그레이션 MIG-1**: `Features/TerminalLauncher`(`wt -w -1 nt` 새창) → **ConPTY spawn으로 대체**(브리프 §6). 외부 wt 열기는 보조 기능 잔류 검토(본 로드맵 범위 밖 옵션).
- **완료 Gate1(출구)**: 렌더① 실시간 갱신 · 탭 전환 시 화면/포커스 활성 · 키보드 입력 포커스(특수키 VT 정확) · 리사이즈 재래핑 · 스크롤백 조회 동작 · 파서-렌더 경계 1곳 확립. `[11]` 터미널 단위·통합 테스트 통과. (렌더②·pane은 Gate1 제외 — 병렬/후속.)
- **대응 RISK**: RISK-002(렌더)·003(파서)·004(스레딩) — P0에서 선제, L0에서 실사용 정착.

### 3-2. L1 — 세션 소유·제어 (SES·PRF) [핵 MVP 완성]

- **목적**: SessionProfile을 런타임 Session으로 기동하고 커맨드 주입·종료·상태추적·다중 관리·영속을 완성. 이 레이어 완료가 **AI 세션 오케스트레이션 정체성**(결정 #1)의 최소 출시 단위.
- **진입 Gate**: **Gate1**(L0 substrate 안정).
- **배치 FR(13)**: SES FR-011·012·013·014·015·016·017(Must) · FR-018(Should) · PRF FR-019·020·023(Must) · FR-021·022(Should).
- **핵심 FN**: FN-SES-01~10 · FN-PRF-01~07.
- **선행 ENT**: **ENT-005 session_profile([P] 중심 애그리거트)** → ENT-006·007·008(임베드 자식) → ENT-004(session 런타임, profile value-copy). 데이터 선행성: 프로파일 영속(09 §8-4 JSON-first)이 세션 기동의 입력.
- **산출물**: ① 프로파일 CRUD·검증·JSON 원자 영속(temp→rename, NFR-011) · ② 프로파일→세션 기동 오케스트레이션(cwd·초기명령·claude 자동실행 적용) · ③ 임의 세션 커맨드 주입(입력 파이프 재사용, ≤100ms) · ④ 정상/강제 종료·상태 머신(starting/running/exited/error)·크래시 격리 · ⑤ 다중 세션 함대 목록·활성 전환·탭 동기 · ⑥ Session.as 정의·전파 · ⑦ 복제·삭제·프리셋·재시작(후반).
- **마이그레이션 MIG-2**: `Features/SessionMonitor`(`ProcessTracker` 전역 감지) → **폐기, 앱-소유 `SessionManager`(함대 소유)로 대체·흡수**(브리프 §6·10 §4-2). FR-017·039·016이 이 전환에 의존 — L0→L1 전이의 핵심 단계.
- **후속숙제 해소**: **①** 영속화 형태 = 09 §8-4에서 **JSON-first(SQLite-ready)** 확정 → L1에서 반영·확인.
- **완료 Gate2(=MVP 출시선)**: 프로파일 기반 기동·claude 자동실행·커맨드 주입(≤100ms)·다중세션 목록/탭·프로파일 영속(재시작 후 유지, 원자적)·**크래시 격리 100%**(NFR-009)·동시 세션 ≥8 선형 열화 이내(NFR-012). ClickOnce MVP 게시 준비(FR-041). `[11]` 세션 lifecycle·주입·영속 회귀 통과. → **MVP 출시 가능**.
- **대응 RISK**: RISK-006(세션 lifecycle, P0 상태전이 검증 흡수)·009(영속 원자성).

### 3-3. L2 — IPC 오케스트레이션 (IPC) [채널 협업]

- **목적**: skill_ipc_control의 GUI 프론트엔드로서 채널 watch/read·송신·멤버십·스킬 트리거 주입·대화 뷰·1:1 매핑 제공. **IPC 재구현 금지**(C4·NFR-017), 파일 계약만 소비.
- **진입 Gate**: **Gate2(MVP)** + **요구확정 ④·⑧**(채널 1:1 매핑 규칙·위험 가드 정책) — 착수 전 확정 필요.
- **배치 FR(6)**: FR-024·025·026·027·029(Must) · FR-028(Should, 후반).
- **핵심 FN**: FN-IPC-01~07.
- **선행 ENT**: ENT-009 channel_ref·ENT-010 member_ref·ENT-011 message_ref([X] 투영) + ENT-008 채널멤버십([P], L1 산출). 선행성: L1의 Session.as(FR-023)·프로파일 채널멤버십이 IPC 정체성 키.
- **기능 선행**: FN-TRM-03(입력 파이프)·FN-SES-04(주입) → **FN-IPC-05 스킬 트리거 주입 경로 재사용**. 입력경로가 렌더①(L0)에서 확보되므로 **L2는 렌더②에 볼모잡히지 않음**(브리프 §2·C10) — 이것이 렌더② 병렬화의 근거.
- **산출물**: ① `IIpcFileContract` 읽기전용 어댑터(계약 변동 흡수 1곳, RISK-008) · ② FileSystemWatcher 채널 watch(≤2s)·방어적 read · ③ send.cmd 재사용 송신 · ④ 멤버십 관리·stale 복구(NFR-010) · ⑤ 트리거 프롬프트 주입 · ⑥ 채널 대화 뷰(후반) · ⑦ 앱 채널↔`channels/<ch>` 1:1 매핑.
- **후속숙제 해소**: **④** 채널 1:1 매핑 최종 확정(진입 전) · **⑧** 위험 가드 정책 범위 확정(FR-037 활성, 아래 X 참조) · **⑥** web_monitor 흡수 vs 병존 정리(FR-028 대화 뷰 시점).
- **완료 Gate3(출구)**: 채널 watch 반영 ≤2s · send.cmd 왕복 · 멤버십·stale 복구 · 스킬 트리거 주입 후 세션 skill_ipc_control 발동 · 1:1 매핑 일관 · **재구현 0**(NFR-017 감사). `[11]` IPC 계약·watch·주입 통과.
- **대응 RISK**: RISK-008(파일 계약 결합)·001(주입 경로).

### 3-4. L3 — 관측·자산 (OBS·AST) [확장]

- **목적**: 세션 jsonl 트랜스크립트 토큰 집계(관측) + `~/.claude` 자산 트리 읽기/쓰기(편집). 안정화 후 적층하는 확장 레이어.
- **진입 Gate**: **Gate3**(L2 통과) + **요구확정 ⑤**(프롬프트 편집 v1 범위 — 읽기/쓰기 경계·검증 후속 여부).
- **배치 FR(7)**: OBS FR-030·031(Must)·032(Should) · AST FR-033·034·035(Must)·036(Should).
- **핵심 FN**: FN-OBS-01~03 · FN-AST-01~04.
- **선행 ENT**: ENT-012 transcript_ref([X])·ENT-013 token_usage_snapshot([C] 파생캐시+증분커서)·ENT-014 asset_node([X] write-through). 선행성: OBS는 L1의 Session.as↔jsonl 매핑·claude 자동실행(FR-013, 트랜스크립트 생성원)에 의존. AST는 OS 파일시스템 독립(기술적으로 조기 가능하나 우선순위상 L3).
- **산출물**: ① jsonl 경로 탐지·Session 매핑(부재 graceful) · ② Utf8JsonReader 증분 파싱·오프셋 커서·세션 누적 집계(≤500ms/100MB) · ③ 세션별 토큰 표시(후반) · ④ 자산 트리 브라우저(경계 가드 FN-SEC-04) · ⑤ 파일 읽기 에디터 · ⑥ 원자적 쓰기·저장 · ⑦ 파일 CRUD(후반).
- **후속숙제 해소**: **⑤** 편집 v1 범위 확정(읽기/쓰기까지, 검증·스키마 후속).
- **완료 Gate4(출구)**: jsonl 탐지·토큰 증분 집계(≤500ms/100MB, 방어적 파싱 NFR-020) · 자산 트리 로드·읽기·원자적 쓰기(경로 탈출 0, NFR-008) · 세션별 토큰 표시. `[11]` 증분 파싱·경로 가드 통과.
- **대응 RISK**: RISK-007(jsonl 포맷 변동)·009(자산 저장 원자성).

### 3-5. X — 횡단 (SEC·SYS) [상시 편입]

- **목적**: 특정 레이어가 아니라 **전 레이어에 상시 편입**되는 보안 정책·앱 셸·배포·설정·진단. 핵심 정책은 초기(L0/L1)부터 baseline으로 강제.
- **진입 Gate**: 상시(각 레이어 Gate에 부분 편입).
- **배치 FR(7)**: SEC FR-038·039(Must, L0/L1 baseline)·037(Should, L2 前) · SYS FR-040(Must, L0 baseline)·041(Must, MVP@Gate2)·042(Should, L2/L3)·043(Could, **M-RESTORE 후속**).
- **핵심 FN**: FN-SEC-01~04 · FN-SYS-01~05.
- **선행 ENT**: ENT-015 app_settings·ENT-016/017 last_layout·ENT-018 diagnostics_log([P] 영속).
- **레이어별 편입 시점**:
  - **L0 baseline**: FR-040(셸 레이아웃 — 터미널 존 호스팅)·FR-038(포트 0 — 수신 소켓 미생성)·FR-039(앱-소유 범위 — SessionManager 소유 핸들만, MIG-2 연동)·FN-SYS-05(진단 로그 상시).
  - **Gate2(MVP)**: FR-041(ClickOnce MVP 게시, RISK-010) — 배포 파이프라인은 L0부터 준비, MVP에서 정식 게시.
  - **L2 前**: FR-037 위험 커맨드 가드 활성(주입 FR-014/027 광범위 사용 직전, 후속숙제 ⑧ 정책 확정 후) · FN-SEC-04 경로 가드(L3 자산 편집 전 필수).
  - **L2/L3**: FR-042 앱 설정(경로·기본 프로파일·렌더 단계·가드 on/off).
  - **후속 M-RESTORE**: FR-043 상태 복원(프로파일 재기동 제안, FN-SES-10 활용).
- **완료 기준**: 각 소속 Gate에서 부분 충족(포트 0·경로 탈출 0·소유 범위 강제·원자 저장·진단 로깅). NFR-006/008/011/022 검증은 10 §9·11 게이트 연계.

### 3-6. 병렬 트랙 (렌더② · pane · 상태복원)

| 트랙 | FR | 진입 | 근거·완료 | 후속숙제 |
|---|---|---|---|---|
| **M-R2 렌더②** | FR-005 (FN-TRM-07) | Gate1 + MVP 안정 | **L2와 병렬**(입력경로 렌더①확보로 볼모 아님, C10·브리프 §2). alt-screen/claude TUI 렌더 정확도 — RISK-005 PoC, claude TUI 깨짐 0. 미완 구간엔 SC-08 "TUI 깨짐 경고 배너 + 주입/제어 가능" 표기 | **⑦** 렌더② 마일스톤 시점 = 본 트랙으로 확정 |
| **M-PANE pane 분할** | FR-008 (FN-TRM-11) | Gate2(MVP) 이후 | Could — 탭 내 수평/수직 분할. 안정화 후 여력 배치(C8) | — |
| **M-RESTORE 상태복원** | FR-043 (FN-SYS-04) | Gate4 이후 | Could — ConPTY 세션은 복원 불가 → 프로파일 기반 재기동 제안. last_layout 스냅샷 활용 | ① 영속(09 해소) |

### 3-7. 상대 일정 (ASCII 간트 — Gate 앵커, 달력 아님)

```
 상대구간:  S0    S1    S2    S3    S4    S5    S6    S7    S8    S9
 ---------|-----|-----|-----|-----|-----|-----|-----|-----|-----|-----
 P0 PoC   |#####|                                                        Gate0
 L0 TRM   |     |###########|                                            Gate1
 L1 SES/PRF           |###################|  <MVP>                       Gate2
 L2 IPC                                 |###########|                    Gate3
 L3 OBS/AST                                       |###########|          Gate4
 --병렬--------------------------------------------------------------
 M-R2 렌더(2)                           |.........#####.........|        (L2와 병렬)
 M-PANE pane                                        |....후속....|
 M-RESTORE 복원                                            |..후속..|
 --횡단(상시)---------------------------------------------------------
 X SEC/SYS |=====정책 baseline(L0/L1)=====|==ClickOnce@MVP==|==설정/복원==|
```
> `#`=핵심 산출 구간, `.`=선행/병렬 준비, `=`=상시. S0~S9는 **상대 순서**일 뿐 달력·주차가 아니며 Gate 충족 시 다음 구간으로 진행(§5).

---

## 4. 의존성·선행 조건

### 4-1. 적층 의존 그래프 (ASCII)

```
                         [요구확정 (4)(8)]        [요구확정 (5)]
                               |                        |
 P0 --Gate0--> L0 --Gate1--> L1 --Gate2(MVP)--> L2 --Gate3--> L3 --Gate4-->
  |            |             |                   |            |
  |            |             +--(as/멤버십 키)-->+            |
  |            |             +--(as<->jsonl 매핑)-------------+
  |            +--(ITerminalScreenModel 경계)                 |
  |            +--(입력 파이프: 렌더(1) 확보)--> 렌더(2) 병렬 --+ M-R2
  |
  +--(VtNetCore 확정 (2) / 렌더 목표 (3))
  |
 [MIG-1 wt->ConPTY : P0/L0]        [MIG-2 ProcessTracker->SessionManager : L0->L1]
  |
 [횡단 X : FR-040 shell(L0) -> FR-038/039 SEC(L0/L1) -> FR-041 ClickOnce(MVP) -> FR-037 guard(L2前) -> FR-042 설정 -> FR-043 복원(후속)]
```

### 4-2. 선행성 3축 근거 (적층 순서의 정당화)

- **데이터 선행(ENT)**: `ENT-005 프로파일[P]` → `ENT-004 세션[R]`(value-copy) → { `ENT-009~011 채널[X]`(as·멤버십 키), `ENT-012~013 토큰[X][C]`(as↔jsonl) }. 프로파일 영속(09 JSON-first)이 세션의 입력, 세션 as가 채널·토큰의 조인 키 → **L1이 L2·L3보다 선행**.
- **기능 선행(FN)**: `FN-TRM-03 파이프` → `FN-SES-04 주입` → `FN-IPC-05 스킬 트리거`(경로 재사용). 입력경로가 렌더①에서 확보 → **L2 주입이 렌더②에 독립**(병렬화 가능). `FN-SES-03 claude 자동실행`(트랜스크립트 생성원) → `FN-OBS-01 탐지` → **L3 관측이 L1에 선행 의존**.
- **리스크 선행(RISK)**: RISK-001/002/003/004(★최상, L0 substrate·렌더)가 임계경로 → **P0 선제**. RISK-006→L1, 008→L2, 007→L3, 005→렌더② 트랙, 009/010→횡단.

### 4-3. 진입 선행조건 체크리스트 (Gate별)

| Gate | 선행 산출(필수) | 선행 확정(요구/의사결정) |
|---|---|---|
| Gate0 | P0 vertical-slice 5실험 통과 | 후속숙제 ②③ 해소(파서·렌더 목표) |
| Gate1 | 렌더①·탭·입력·리사이즈·스크롤백·경계 인터페이스 | MIG-1(wt→ConPTY) 완료 |
| **Gate2(MVP)** | 프로파일 기동·주입·다중세션·영속·격리 | 후속숙제 ① 반영(JSON-first)·MIG-2(SessionManager) 완료 |
| Gate3 | 채널 watch·송신·멤버십·주입·매핑·재구현0 | 후속숙제 **④⑧** 확정(진입 전)·⑥ 정리 |
| Gate4 | 토큰 증분 집계·자산 읽기/쓰기·경로 가드 | 후속숙제 **⑤** 확정(편집 범위) |
| M-R2 | alt-screen/claude TUI 렌더 정확 | 후속숙제 **⑦** = 본 트랙 시점 확정 |

### 4-4. 기존 코드 마이그레이션 (전환 단계)

| MIG | 대상(현재) | 전환 | 배치 시점 | 영향 FR |
|---|---|---|---|---|
| MIG-1 | `Features/TerminalLauncher`(`wt -w -1 nt` 새창) | ConPTY spawn 앱-소유로 **대체** (외부 wt는 보조 잔류 검토) | P0 실증 → L0 정착 | FR-001·002 |
| MIG-2 | `Features/SessionMonitor`(`ProcessTracker` 전역 감지) | 앱-소유 `SessionManager` 함대로 **폐기·대체·흡수** | L0→L1 전이 | FR-017·039·016 |
| (유지) | `Shell/Features/Shared` 구조 · `Shared/Core`(ViewModelBase·RelayCommand) | **계승**(신규 기능 Feature 모듈 확장, C9/NFR-016) | 전 구간 | FR-040 |

---

## 5. 일정 표기 원칙 (상대 일정·Gate)

- **캘린더 날짜·주차 고정 비사용**: 본 로드맵은 착수일·마감일을 못박지 않는다. 진행 단위는 **Gate 충족**이다. 각 단계는 **진입 Gate가 충족되면 착수**하고, **출구 Gate(완료조건)를 통과하면** 다음 레이어로 진행한다.
- **상대 구간(S0~Sn)**: Gate에 앵커된 상대 순서일 뿐이며 병렬 트랙(렌더②)·횡단(SEC/SYS)과 겹칠 수 있고 재배열 가능하다.
- **Gate = 완료조건 + 선행확정**: §3의 각 완료 Gate(관찰 가능 통과 기준·정량치)와 §4-3의 선행조건을 모두 충족해야 통과로 판정. 정량 목표(렌더 fps·주입 지연·집계 시간)는 10 §7 PoC 실측과 11 테스트 게이트에서 확정·검증한다.
- **임계경로 우선**: P0(RISK-001~004)가 막히면 상위 레이어 착수 금지 — Gate0 재도전(파서 교체·렌더 최적화) 후 L0 상대 일정 재산정.
- **MVP 우선 출시**: Gate2(L0+L1)에서 v1 MVP 출시 가능. L2·L3·병렬 트랙은 MVP 이후 Gate 기반 적층.

---

## 문서 메타

- 버전: v1.0 / 생성일: 2026-07-01
- 담당: plan_roadmap_planner · 깊이: deep · 발번 ID **없음**(참조 전용 — REGISTRY_APPEND 없음)
- 로드맵 지표: 5 단계(P0·L0·L1·L2·L3) + 횡단 X + 병렬 3트랙 · Gate0~4 · **FR 전수 매핑 43/43(미배치 0)** · RISK-001~004 P0 선제 · 후속숙제 ①~⑧ 전건 배치.
- 관련 문서: [`04_requirements`](./04_requirements.md)(FR·MoSCoW·후속숙제) · [`05_functions`](./05_functions.md)(FN 단계 산출단위) · [`09_database`](./09_database.md)(ENT 선행성·JSON-first) · [`10_tech`](./10_tech.md)(RISK·압축 PoC §7-11·아키텍처) · [`11_test`](./11_test.md)(완료 Gate 테스트 연계) · [`00_meeting_brief`](./00_meeting_brief.md)(4레이어·결정로그).
- 미해결·후속 → `13_followups`: ⑦(렌더② 시점)=**본 문서 M-R2 트랙으로 확정** · ①②③④⑤⑥⑧은 각 Gate 선행확정으로 배치(§4-3). 잔여 운영 확인(코드 서명 인증서·web_monitor 흡수 결정·pane/복원 Could 착수 판단)은 13으로 이관.
