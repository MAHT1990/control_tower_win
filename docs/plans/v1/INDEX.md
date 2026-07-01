# Control Tower v1 기획 산출물 — INDEX

> 담당: orchestrator(합성) · skill_plan v1 · 14문서 · 2026-07-01
> 역설계 기획: 기존 산출물(Notion 메모·docs/guides·docs/runbooks·현재 소스) + 사전 회의 3라운드 종합(`00_meeting_brief.md`)을 입력으로 도출.

---

## 한 줄 정의

**Control Tower** — 로컬에서 여러 Claude Code/PowerShell 세션을 **앱이 직접 소유(ConPTY 임베드)** 하여, 의도별 **세션 프로파일**로 띄우고 · **IPC 채널**로 묶어 협업시키며 · **토큰·프롬프트 자산**까지 한 곳에서 관제하는 **단일 파워유저용 AI 세션 관제탑** (WPF/.NET 10 데스크톱).

## 핵심 컨셉 (5축)

① ConPTY 앱-소유 터미널(substrate) · ② 세션 프로파일 애그리거트 · ③ IPC 오케스트레이션(skill_ipc_control 프론트엔드) · ④ 토큰 관측(jsonl 파싱) · ⑤ 프롬프트 자산 편집(~/.claude). **차별축**: 세션 간 IPC 협업 — 경쟁(worktree 격리)에 부재.

---

## 문서 구성표

| # | 문서 | 내용 | 담당 | 핵심 지표 |
|---|---|---|---|---|
| — | [`00_meeting_brief`](./00_meeting_brief.md) | 사전 회의 3라운드 종합(입력 브리프) | (회의) | 결정 로그 11 · 로드맵 4레이어 |
| 01 | [`01_overview`](./01_overview.md) | 제품 개요·정체성·핵심컨셉·범위·성공기준 | 합성 | 5축·10제약 |
| 02 | [`02_market`](./02_market.md) | 시장 분석·경쟁·Build vs Buy | competitor | 조사 13·Build/Buy 8 |
| 03 | [`03_users`](./03_users.md) | 사용자 유형·페르소나(운영 모드) | user_classifier | UT 5 · P 1 |
| 04 | [`04_requirements`](./04_requirements.md) | 요구사항 FR/NFR·8카테고리·제약 | requirement_analyzer | **FR 43 · NFR 22** |
| 05 | [`05_functions`](./05_functions.md) | 기능정의 FN·추적성 매트릭스 | function_specifier | **FN 53** (FR 전수 커버) |
| 06 | [`06_behaviors`](./06_behaviors.md) | 행동 시나리오·Journey Map | behavior_designer | BS 22 · JM 5 · 엣지 15 |
| 07 | [`07_interfaces`](./07_interfaces.md) | IA·화면 SC·흐름·UX | interface_designer | **SC 22** (FR 커버 43/43) |
| 08 | — (제외) | REST/DTO — 로컬 데스크톱, 서버 API 없음 | (excluded) | in-proc 계약은 10 |
| 09 | [`09_database`](./09_database.md) | 데이터 모델·ERD·ENT·영속화 | db_modeler | **ENT 18** (영속 9/투영 5/런타임 4) |
| 10 | [`10_tech`](./10_tech.md) | 기술 스택·아키텍처·리스크·PoC | tech_researcher | **RISK 10** · NFR 22/22 |
| 11 | [`11_test`](./11_test.md) | 테스트·QA 전략·품질 게이트 | test_strategist | TC 131 · 게이트 9 |
| 12 | [`12_roadmap`](./12_roadmap.md) | 실행 로드맵·레이어·일정 | roadmap_planner | 5단계 · FR 매핑 43/43 |
| 13 | [`13_followups`](./13_followups.md) | 후속 숙제·운영 사전확인 | 합성 | 숙제 8 · 운영 5 |

> **08 제외 근거**: 본 제품은 로컬 데스크톱으로 REST 백엔드가 없다(수신 포트 0). 통합 계약(ConPTY interop·IPC 파일·jsonl 스키마)은 10 tech가 다룬다.

---

## 권장 읽기 순서

1. **개요 파악**: `00_meeting_brief` → `01_overview` → `INDEX`(본 문서)
2. **무엇을·왜**: `04_requirements`(FR/NFR) → `03_users` → `06_behaviors`
3. **어떻게 보이나**: `07_interfaces`(SC)
4. **어떻게 만드나**: `05_functions`(FN) → `09_database`(ENT) → `10_tech`(스택·RISK)
5. **어떻게 검증·실행**: `11_test` → `12_roadmap` → `13_followups`
6. **시장 맥락**: `02_market`(언제든)

---

## 요약 지표

- **식별자 총계**: FR 43 · NFR 22 · FN 53 · UT 5 · P 1 · BS 22 · JM 5 · SC 22 · ENT 18 · RISK 10 · 공통 카테고리 8(TRM·SES·PRF·IPC·OBS·AST·SEC·SYS)
- **MoSCoW(FR)**: Must 30 · Should 11 · Could 2
- **레이어 로드맵**: P0(압축 PoC) → L0(터미널) → L1(세션·프로파일=MVP) → L2(IPC) → L3(관측·자산) + 횡단(SEC·SYS)
- **핵심 결정(v2)**: **EasyWindowsTerminalControl(공식 WT 렌더러 임베드, INTEGRATE/BUY, MIT)** — runbook 06 GO(10 v2, 엔진 반전) · self-build(ConPTY+VtNetCore+GlyphRun)는 NO-GO 폴백 · JSON-first 영속 · IPC 재사용(REUSE)
- **무결성**: FR 전수 커버리지 FN/SC/ENT/로드맵 **43/43** · 고아 참조 0 · 중복 ID 0

---

## 추적성 매트릭스 (FR ↔ FN ↔ SC ↔ ENT — 완성)

> 05 §2 매트릭스의 SC·ENT 열을 07·09 결과로 완성. 모든 FR이 ≥1 FN·SC·ENT로 매핑됨(전수 커버리지).

### TRM — 터미널 (L0)
| FR | FN | SC | ENT |
|---|---|---|---|
| FR-001 | TRM-01·02 | 07·08·14·04 | 004 |
| FR-002 | TRM-03 | 08 | 004 |
| FR-003 | TRM-04 | 08 | 001 |
| FR-004 | TRM-05·06 | 08 | 001·002 |
| FR-005 | TRM-07 | 08 | 001 |
| FR-006 | TRM-08 | 07 | 003 |
| FR-007 | TRM-09·10 | 08 | 002·004 |
| FR-008 | TRM-11 | 10 | 003 |
| FR-009 | TRM-12 | 08 | 004 |
| FR-010 | TRM-13 | 09·08 | 001 |

### SES — 세션 제어 (L1)
| FR | FN | SC | ENT |
|---|---|---|---|
| FR-011 | SES-01 | 14 | 005·004·008 |
| FR-012 | SES-02 | 14·15 | 005·006 |
| FR-013 | SES-03 | 15·14 | 005 |
| FR-014 | SES-04 | 12 | 004·018 |
| FR-015 | SES-05·06 | 11·07·04 | 004 |
| FR-016 | SES-07·08 | 11·06 | 004·018 |
| FR-017 | SES-09 | 11 | 004 |
| FR-018 | SES-10 | 11·03 | 005·004 |

### PRF — 프로파일 (L1)
| FR | FN | SC | ENT |
|---|---|---|---|
| FR-019 | PRF-01·02 | 15 | 005·006·007·008 |
| FR-020 | PRF-03 | 14 | 005 |
| FR-021 | PRF-04·05 | 14 | 005 |
| FR-022 | PRF-06 | 14 | 005 |
| FR-023 | PRF-07 | 15 | 005·004 |

### IPC — 채널·협업 (L2)
| FR | FN | SC | ENT |
|---|---|---|---|
| FR-024 | IPC-01·02 | 16 | 009·010 |
| FR-025 | IPC-03 | 18 | 011 |
| FR-026 | IPC-04 | 16 | 008·010 |
| FR-027 | IPC-05 | 19 | 007·004 |
| FR-028 | IPC-06 | 17 | 011 |
| FR-029 | IPC-07 | 16 | 009·015 |

### OBS — 관측 (L3)
| FR | FN | SC | ENT |
|---|---|---|---|
| FR-030 | OBS-01 | 20 | 012 |
| FR-031 | OBS-02 | 20 | 013·012 |
| FR-032 | OBS-03 | 20·11 | 013 |

### AST — 자산 편집 (L3)
| FR | FN | SC | ENT |
|---|---|---|---|
| FR-033 | AST-01 | 21 | 014·015 |
| FR-034 | AST-02 | 22 | 014 |
| FR-035 | AST-03 | 22 | 014 |
| FR-036 | AST-04 | 21 | 014 |

### SEC / SYS — 횡단 (X)
| FR | FN | SC | ENT |
|---|---|---|---|
| FR-037 | SEC-01 | 13 | 015 |
| FR-038 | SEC-02 | 02 | 015 |
| FR-039 | SEC-03 | 11·04 | 004 |
| FR-040 | SYS-01 | 01 | 016 |
| FR-041 | SYS-02 | 05 | 015 |
| FR-042 | SYS-03 | 02 | 015 |
| FR-043 | SYS-04 | 03 | 016·017·005 |

> **매트릭스 무결성**: 43개 FR 전부가 ≥1 FN·≥1 SC·≥1 ENT로 매핑됨(미커버 0). NFR-only 실현 기능 FN-SEC-04(NFR-008)·FN-SYS-05(NFR-022)는 FR 소급이 아니라 별도(05 §11-3).

---

## 문서 메타

- 버전: v1.0 / 생성일: 2026-07-01 / 담당: orchestrator(skill_plan)
- 깊이: deep · 출력: file(`docs/plans/v1/`) · 제외 단계: 08 api_designer
- 산출 무결성: 검증 게이트 통과(FR 전수 커버리지 · 고아 참조 0 · 중복 0 · 카테고리 단일)
- 다음 스킬: `skill_build`(구현) · `skill_sampler`(샘플) — 입력: `docs/plans/v1/`
- 미해결: 후속 숙제 8건 → [`13_followups`](./13_followups.md) (미해결 3건 ④⑥⑧은 L2 진입 전 확정)
