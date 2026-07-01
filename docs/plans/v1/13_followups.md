# 13. 후속 숙제·운영 사전확인 (Follow-ups & Open Issues)

> 담당: orchestrator(합성) · 깊이: deep · 열린 결정 3건 + 확정 참조 5건 + 운영 사전확인 6건
> 본 문서는 구현 착수 전 확정이 필요한 열린 항목과, 이미 상위 문서에서 확정된 결정의 참조점, 그리고 운영 사전확인 항목을 취합한다.

---

## 0. 개요

기획 산출 과정에서 도출된 항목을 세 부류로 정리한다 — (1) 아직 열려 있어 사용자/구현 확정이 필요한 항목, (2) 이미 상위 문서에서 확정된 결정(참조점), (3) 구현·배포 전 확보가 필요한 운영 항목.

---

## 1. 열린 결정 (사용자 확정 필요 — L2/L3 진입 전)

MVP(L0+L1) 착수는 막지 않으며, 아래는 해당 레이어 진입 전 확정한다.

| # | 항목 | 질문 | 잠정 방향 | 관련 FR | 확정 시점 |
|---|---|---|---|---|---|
| A | 앱 채널 ↔ IPC channel 매핑 | 앱 Channel을 `channels/<ch>/`와 1:1로 볼지? 그룹 라우팅(`a,b,c`)은 어떻게 표현? | 1:1 기본 + 그룹 발신은 `to` 컨벤션, 멤버십은 채널 단위 | FR-024·026·029 | L2 진입 전 |
| B | web_monitor 흡수 vs 병존 | 기존 `tools/web_monitor`를 앱이 내재화할지, 병존시키고 파일 관측만 할지? | 앱 내 경량 대화 뷰(SC-17) 제공 + web_monitor 병존 | FR-028·024 | L2 |
| C | 위험 커맨드 가드 정책 | 위험 패턴 목록·on/off·확인 UX 범위? | 최소 가드 기본 on, 다중 세션 broadcast 주입에 우선 적용 | FR-037·NFR-007 | L2 진입 전 |

---

## 2. 확정된 결정 (참조점)

이미 상위 문서에서 확정되어 열린 항목이 아니다. 구현 시 해당 문서를 참조한다.

| 항목 | 결정 | 참조 |
|---|---|---|
| 세션 프로파일 영속화 | JSON 파일 + 원자적 temp→rename (토큰이력·진단 축적 시 SQLite 승격) | 09 §8-4 |
| 터미널 엔진 | EasyWindowsTerminalControl(공식 WT 렌더러 임베드) · self-build는 폴백 | 10 TS-02/03 |
| 터미널 파싱/렌더/성능 | 공식 Windows Terminal 렌더러가 담당(자체 파서·셀 렌더러 불요) | 10 TS-02 |
| alt-screen/TUI 렌더 | 엔진 기본 제공(별도 렌더 단계 불요) | 10 TS-02 |
| 프롬프트 편집 v1 범위 | 트리 브라우저 + 읽기/쓰기(Must)·생성/삭제/이름변경(Should), 스키마 검증은 후속 | 04 FR-033~036 |

---

## 3. 운영 사전확인 (구현·배포 전 확보)

| # | 항목 | 내용 | 근거 | 시점 |
|---|---|---|---|---|
| O1 | Windows CI 러너 | ConPTY·WPF UI 자동화는 리눅스 헤드리스 불가 → Windows CI 러너가 INTG·SEC·E2E 전제 | 11 §6-2 | L0 착수 전 |
| O2 | native 자산 배포 검증 | ClickOnce 산출물에 conpty.dll·OpenConsole.exe·WT 렌더러 동봉 확인(빈 터미널 방지) | 10 RISK-001·010 | MVP 게시 전 |
| O3 | 코드 서명 인증서 | SmartScreen 완화용 OV/EV 인증서 | 10 §8-1 | MVP 게시 전 |
| O4 | 엔진 라이선스·버전 핀 | EasyWindowsTerminalControl(MIT) 재배포 조건 확인 · `CI.Microsoft.*` beta 버전 정확히 핀 | 10 RISK-003 | L0 착수 전 |
| O5 | claude jsonl 실 스키마 샘플 | 토큰 파싱·경로 탐지 규칙 검증용 실제 트랜스크립트 샘플(방어적 파싱 전제) | 10 RISK-007 | L3 착수 전 |
| O6 | Could 착수 판단 | pane 분할(FR-008)·상태 복원(FR-043)의 여력 기반 착수 결정 | 12 병렬 트랙 | MVP 이후 |

---

## 4. 리스크 요약 (RISK → 완화 → 검증)

10이 발번한 RISK 10건 중 임계경로:

| RISK | 우선 | 완화 요지 | 검증 |
|---|:--:|---|---|
| RISK-001 native 배포 함정 | ★최상 | csproj RID·UseRidGraph·GeneratePathProperty·`<None>` conpty.dll/OpenConsole.exe 복사 | 클린 빌드 native 존재 게이트 |
| RISK-002 airspace 오버레이 제약 | 상 | 경고배너·로그·게이트는 터미널 존 밖 별도 패널/모달 | 컨텍스트 메뉴 OK 확인 |
| RISK-003 엔진 의존(beta·라이선스) | 중 | CI 버전 핀·`ITerminalSession` 경계·라이선스 확인·폴백 격리 | 핀 재현·라이선스 게이트 |
| RISK-004 주입/캡처 스레딩 | 중 | 캡처 델리게이트 Dispatcher 마샬링·`WriteToTerm` 주입-타이핑 공존 | 통합 PoC |
| RISK-006 세션 수명·좀비 정리 | 중 | `RestartTerm`/`DisconnectConPTYTerm`·창 닫힘 자식 정리 | 100회 spawn/close 좀비 0 |

→ 임계경로는 **native 배포(RISK-001)**. 나머지(파싱·렌더·alt-screen)는 공식 WT 렌더러가 담당하므로 substrate 리스크가 낮다.

---

## 문서 메타

- 버전: v1.0 / 생성일: 2026-07-01 / 담당: orchestrator(합성)
- 관련 문서: [`04_requirements`](./04_requirements.md) · [`09_database`](./09_database.md)(영속) · [`10_tech`](./10_tech.md)(엔진·RISK) · [`12_roadmap`](./12_roadmap.md)(레이어·게이트) · [`INDEX`](./INDEX.md)
