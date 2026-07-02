# 02. 시장 분석 (Market Analysis · Competitive Landscape · Build vs Buy)

> 담당: plan_competitor_researcher · 깊이: deep · 조사 13개(국내 0 / 해외 13) · 카테고리 4축
> 본 문서는 터미널 substrate·AI 세션 오케스트레이션·토큰 관측·VT 렌더 컨트롤 4축의 시장·경쟁을 조사해 Control Tower v1의 Build vs Buy 결정과 설계 반영점을 외부 근거로 뒷받침한다.

---

## 0. 요약 (Executive Summary)

- **시장 맥락**: 2026년은 애널리스트 다수가 "단일 에이전트 → 다중 에이전트 오케스트레이션 breakthrough year"로 지목한다. 엔터프라이즈 AI 코딩 에이전트 시장은 연환산 약 $9.8B~$11.0B, 에이전트 워크플로가 "중앙 조율 하 전문 에이전트 협업"으로 이동 중이다.[^gartner][^anthropic] Control Tower의 "여러 Claude 세션을 한 곳에서 관제" 컨셉은 이 트렌드의 **로컬·단일 파워유저** 변형이다.
- **경쟁 지형 3분할**: ① 터미널/멀티플렉서(Windows Terminal·VS Code·tmux·WezTerm·Warp) = substrate 벤치마크, ② AI 세션 오케스트레이터(Claude Squad·Conductor·Vibe Kanban·Claude Code 네이티브 Agent Teams) = 직접 경쟁, ③ 토큰 관측(ccusage) = 인접 유틸.
- **결정적 공백(우리의 자리)**: 모든 오케스트레이터는 **git worktree 격리** 모델이다 — 에이전트가 서로 밟지 않게 **분리**하는 데 집중하고, **세션이 서로 대화**하지 않는다. Control Tower의 `skill_ipc_control` 기반 **세션 간 IPC 채널 협업**(FR-024~029)은 조사한 13개 중 어디에도 없는 차별 축이다.
- **플랫폼 공백**: Conductor는 macOS 전용, Claude Squad는 TUI, Vibe Kanban은 웹. **Windows 네이티브 데스크톱 관제탑**(WPF)은 비어 있는 세그먼트다.
- **Build vs Buy 핵심 결론(브리프 결정 검증)**:
  - ConPTY 세션 소유·앱-소유·주입 제어 = **BUILD**(자체) — 통제력이 정체성. 브리프 결정 #4 정당.
  - 터미널 렌더 엔진(파싱·셀 렌더·alt-screen) = **BUY/Integrate**(EasyWindowsTerminalControl — 공식 WT 렌더러 임베드) — 자체 렌더러 대공사 회피. 브리프 결정 #6 정당.
  - IPC 채널/relay = **REUSE**(skill_ipc_control 재사용, GUI 프론트엔드만) — NFR-017 재구현 금지.
  - 토큰 = **BUILD-light**(자체 증분 jsonl 파서, ccusage 로직 참고) — jsonl 소스는 업계 표준 접근.
  - 배포 = **BUY**(ClickOnce 계승).
- **최우선 차별화 투자**: (High Impact/High Effort) 세션 간 IPC 협업 + ConPTY 임베드, (High Impact/Low Effort) 토큰 관측 내장·의도별 프로파일 프리셋.

---

## 1. 시장 개관 (Market Overview)

### 1-1. 시장 규모·성장성

- 엔터프라이즈 AI 코딩 에이전트 시장 = 2026년 4월 기준 연환산 약 **$9.8B~$11.0B**; 광의의 에이전틱 AI는 2025 $7.6B → 2026 $10.8B로 확장.[^gartner]
- Gartner: **2028년까지 개발자의 75%가 AI 코딩 에이전트 사용**(2023년 <10%에서 급증), AI 코딩 툴 경험을 요구하는 채용 공고는 2025.01→2026.01 사이 **340% 증가**.[^gartner]
- 시사: 세그먼트 자체는 초고성장이나, 위 수치는 **엔터프라이즈 전반**이다. Control Tower가 겨냥하는 "로컬·단일 파워유저·세션 관제탑"의 직접 시장 수치는 공개 데이터 없음(→ §7).

### 1-2. 핵심 트렌드

1. **단일 → 다중 에이전트 오케스트레이션**: Forrester·Gartner 공히 2026을 다중 에이전트 breakthrough year로 지목. "중앙 조율 하 전문 에이전트 협업" 패러다임.[^anthropic][^gartner]
2. **격리 워크스페이스(worktree) 표준화**: Claude Squad·Conductor·Vibe Kanban 모두 "에이전트마다 격리 git worktree" 모델로 수렴. 병렬 실행 sweet spot은 **3~5 워크스페이스**(Conductor 자체 가이드).[^conductor]
3. **터미널의 에이전트 플랫폼화**: Warp가 터미널을 "에이전트 개발 환경(ADE)"으로 재정의, Agent Management Panel로 탭 전역 에이전트 대시보드 제공(단 최상위 티어 월 $200 논란).[^warp-agents][^warp200]
4. **프론티어 모델 제공자의 앱 레이어 진입**: Claude Code가 **네이티브 Agent Teams**(팀 리드가 태스크 배분·결과 종합)를 내장 — 서드파티 오케스트레이터에 대한 구조적 위협.[^agentteams]
5. **로컬 jsonl 관측 생태계**: ccusage 등 `~/.claude/projects/**/*.jsonl` 파싱 도구군 형성 — 토큰·비용 집계가 로컬 파일 기반으로 표준화.[^ccusage]

### 1-3. 세그먼트 구분

| 세그먼트 | 대표 | 형태 | 협업 모델 | Control Tower 관계 |
|---|---|---|---|---|
| 순수 터미널/멀티플렉서 | Windows Terminal·tmux·WezTerm | 앱/TUI | 사람 수동 | substrate 벤치마크(회피할 함정·UX 관성) |
| 에이전트 터미널 | Warp | 유료 터미널+에이전트 | 관찰·공유 | 기능 참고, 로컬·무료로 차별 |
| 세션 오케스트레이터 | Claude Squad·Conductor·Vibe Kanban | TUI/데스크톱/웹 | **worktree 격리** | 직접 경쟁 — IPC 협업으로 차별 |
| 네이티브 팀 기능 | Claude Code Agent Teams | CLI 내장 | 리드-워커 | 위협·트렌드 근거 |
| 토큰 관측 유틸 | ccusage | CLI | — | 내장 흡수(Build-light) |

### 1-4. 규제·표준 이슈

- 로컬 전용·무인증·단일 유저(브리프 §8) → **개인정보/네트워크 규제 노출 최소**. 앱 수신 포트 0(NFR-006)이 규제·보안 표면을 실질 제거.
- 의존 표준: Windows ConPTY(`CreatePseudoConsole`, Win10 1809+), Claude Code jsonl 트랜스크립트 포맷(비공식·변경 가능 → 방어적 파싱 NFR-020), ClickOnce 배포.

### 1-5. 경쟁 지형 한눈에 (13개)

| ID | 서비스 | 축 | 지역 | 플랫폼 | 모델 | 한줄 정의 |
|---|---|---|---|---|---|---|
| CS-001 | Windows Terminal | 터미널 | 해외(MS) | Win/WinUI3 | 무료 OSS | Windows 표준 탭 터미널·임베더블 TerminalControl |
| CS-002 | VS Code 통합 터미널 | 터미널 | 해외(MS) | 크로스 | 무료 | xterm.js + ConPTY 백엔드 통합 터미널 |
| CS-003 | tmux | 멀티플렉서 | 해외 OSS | Unix | 무료 OSS | 세션/윈도/pane 분리 터미널 멀티플렉서 |
| CS-004 | WezTerm | 터미널+mux | 해외 OSS | 크로스 | 무료 OSS | GPU 가속·내장 멀티플렉서·Lua 설정 |
| CS-005 | Warp | 에이전트 터미널 | 해외(US) | 크로스 | 유료 티어 | 에이전트 관리 패널 내장 ADE(월 $200 티어) |
| CS-006 | Claude Squad | 오케스트레이터 | 해외 OSS | 크로스 TUI | AGPL-3.0 | 다중 AI 에이전트를 worktree별로 묶는 Go TUI |
| CS-007 | Conductor | 오케스트레이터 | 해외(US) | macOS | 무료 BYO | Mac에서 병렬 Claude/Codex worktree 관제 |
| CS-008 | Vibe Kanban | 오케스트레이터 | 해외 OSS | 웹/크로스 | Apache-2.0 | 에이전트 칸반 보드(회사 청산, 커뮤니티 유지) |
| CS-009 | Claude Code Agent Teams | 네이티브 | 해외(Anthropic) | CLI | 구독 | 리드가 태스크 배분·결과 종합하는 내장 팀 기능 |
| CS-010 | ccusage | 토큰 관측 | 해외 OSS | CLI(npm) | 무료 OSS | 로컬 jsonl 파싱 토큰·비용 집계 CLI |
| CS-011 | XtermSharp | .NET 컨트롤 | 해외 OSS | .NET | 무료 OSS | .NET용 Xterm/VT100 에뮬레이터 엔진(렌더 프론트 별도) |
| CS-012 | libvt100 / vtparse 계열 | VT 파서 | 해외 OSS | .NET/C | 무료 OSS | 순수 C# VT100/ANSI 파서(파싱만, 렌더 분리) |
| CS-013 | Windows Terminal Control(embeddable) | .NET 컨트롤 | 해외 OSS | WinUI3/UWP | 무료 OSS | Windows Terminal 코어를 임베드하는 재사용 컨트롤 |

> 국내(한국) 직접 유사 서비스는 검색에서 발견되지 않음 → 인접 해외 오픈소스로 대체 조사(§7 기록).

---

## 2. 핵심 벤치마크 (Deep Benchmarks)

### 2-1. 터미널 / 멀티플렉서 substrate

#### [CS-001] Windows Terminal
- URL: github.com/microsoft/terminal / 운영사 Microsoft / 지역 해외 / 플랫폼 Windows(WinUI3) / 모델 무료 OSS
- 한줄 정의: Windows 표준 탭 터미널. 코어(TerminalControl)는 **재사용 가능 컨트롤**로 설계되어 타 앱 임베드 가능성 존재.[^winui]
- 강점: ConPTY 네이티브 통합·성숙한 VT 렌더·탭. TerminalControl 프로젝트 분리로 임베드 시연됨(Corillian/WindowsTerminal는 WinUI3 임베드 샘플).[^embed]
- 약점(우리에 대한 시사): **WinUI3/UWP 기반** — WPF 앱에 임베드 시 마찰(SwapChainPanel 투명 합성 불가, Win10 일부 기능 제약, UIA 접근성 자체 구현 필요).[^embed][^winui] 앱-소유 세션 lifecycle·임의 주입 제어 API를 노출하지 않음 → 관제탑 요구에 부적합.

#### [CS-002] VS Code 통합 터미널
- URL: code.visualstudio.com/docs/terminal / Microsoft / 크로스 / 무료
- 한줄 정의: xterm.js 프론트 + **ConPTY 백엔드** 통합 터미널.[^vscode]
- 강점: 셸 통합(OSC 시퀀스로 cwd/커맨드 감지), 대량 출력 성능, 다중 터미널.
- 약점(회피할 함정): **ConPTY 뷰포트 소유·리페인트 quirk** 문서화 — ConPTY가 자신을 뷰포트 소유자로 여겨 화면을 재출력함.[^vscode] → 우리 WPF 렌더러(FR-004/005)는 이 리페인트 폭주를 흡수하도록 설계해야(NFR-001). xterm.js는 웹(JS) 스택이라 .NET 직접 재사용 불가.

#### [CS-003] tmux
- URL: tmux OSS / Unix / 무료
- 한줄 정의: 세션·윈도·pane 분리 멀티플렉서(사람 수동 조작).
- 강점: 검증된 pane/윈도 모델, 세션 persistence, 경량.
- 약점: Windows 네이티브 아님, GUI 임베드 불가, AI/세션 프로파일·주입 개념 없음. **Claude Squad가 tmux 위에서 도는 이유**(오케스트레이터가 tmux를 substrate로 씀)이자, 우리가 tmux를 앱-소유 ConPTY로 대체하는 근거.

#### [CS-004] WezTerm
- URL: wezterm.org / OSS / 크로스 / 무료
- 한줄 정의: GPU(wgpu) 가속 + **내장 멀티플렉서**(윈도/탭/pane, persistent 세션) + Lua 설정.[^wezterm]
- 강점: 부드러운 스크롤·렌더 성능 벤치마크, pane/탭 UX 관성, SSH 내장.
- 약점: 범용 터미널이라 AI 세션 오케스트레이션·IPC 협업 없음. 임베더블 .NET 컨트롤 아님. → **렌더 품질의 상향 레퍼런스**로만 참고.

#### [CS-005] Warp
- URL: warp.dev/agents / US / 크로스 / 유료 티어
- 한줄 정의: 터미널을 "에이전트 개발 환경(ADE)"으로 재정의, **Agent Management Panel**로 탭 전역 에이전트 대시보드.[^warp-agents][^warp-multi]
- 강점: 세션=에이전트 대화 다중 실행, 세로 탭(git branch/worktree/PR 메타), 승인·리뷰 루프("80%→100%"), 세션 공유·관찰.
- 약점: **월 $200 티어·클라우드 종속** 논란(로컬·프라이버시 반발).[^warp200] 세션 공유는 "관찰·공유"이지 우리 같은 **세션 간 IPC 메시지 협업**은 아님(§7 확인 필요). → 대시보드 UX·리뷰 루프는 참고, **로컬·무료·무인증**으로 대비 포지셔닝.

### 2-2. AI 세션 오케스트레이션 (직접 경쟁)

#### [CS-006] Claude Squad
- URL: github.com/smtg-ai/claude-squad / OSS(Go) / 크로스 TUI / AGPL-3.0 / ★약 7.9k, v1.0.19(2026-06)[^squad]
- 한줄 정의: Claude Code·Codex·Gemini·Aider·OpenCode·Amp 등 다중 AI 터미널 에이전트를 **격리 git worktree**로 한 TUI에서 관리.
- 강점: 에이전트당 격리 워크스페이스(브랜치 충돌 방지), **named Program 프로파일 + profile picker**(←/→ 전환) — 우리 세션 프로파일(FR-019/022)의 얕은 선례. tmux 산개 대체.
- 약점: **TUI**(GUI 임베드·WPF 렌더 없음), **세션 간 통신 없음**(worktree 격리만), 토큰 관측·프롬프트 자산 편집 없음, AGPL 라이선스.

#### [CS-007] Conductor
- URL: conductor.build / Melty Labs(US, ~6인, Series A $22M) / **macOS 전용** / 무료 BYO[^conductor][^conductor-hn]
- 한줄 정의: Mac에서 병렬 Claude Code/Codex 에이전트를 격리 worktree로 띄우고 diff·PR 리뷰까지 관제.
- 강점: 클릭형 워크스페이스/브랜치 관리, 강력한 diff 뷰어·GitHub PR 플로우, "**3~5 병렬이 sweet spot**" 명시 UX 가이드, 로컬 실행.
- 약점(우리 기회): **macOS 전용**(Windows 공백), worktree 격리라 **세션 간 협업 없음**, 세션 프로파일 개념 얕음(worktree 중심), 토큰/프롬프트 관측 없음.

#### [CS-008] Vibe Kanban
- URL: github.com/BloopAI/vibe-kanban / Apache-2.0 / 웹·크로스 / **회사 청산(2026-04), 커뮤니티 유지**[^vibe]
- 한줄 정의: 에이전트를 칸반 보드로 배치·지시하는 에이전트-agnostic 오케스트레이터.
- 강점: 팀 친화 보드 UX, 오픈소스·자가호스팅, 에이전트 무관.
- 약점: 웹 보드(터미널 임베드·앱 소유 아님), 회사 sunset(모멘텀 리스크), 세션 간 IPC 협업 없음, 로컬 데스크톱 관제 아님.

#### [CS-009] Claude Code 네이티브 Agent Teams
- URL: code.claude.com/docs/en/agent-teams / Anthropic / CLI 내장 / 구독[^agentteams]
- 한줄 정의: 한 세션이 팀 리드로 다수 Claude Code 인스턴스를 조율(태스크 배분·결과 종합)하는 **내장** 기능.
- 강점: 공식·모델 제공자 직접 제공, 설치 불필요, 리드-워커 조율.
- 약점(경계): CLI 내부 조율이라 **GUI 관제탑·세션 프로파일·IPC 채널 뷰·토큰 UI 없음**. → 우리는 이를 **대체가 아니라 상위 관제 레이어**로 흡수(세션들을 시각·관측·주입). 단, 네이티브 확장은 장기 위협(§7·트렌드 근거).

### 2-3. 토큰 관측

#### [CS-010] ccusage
- URL: github.com/ryoppippi/ccusage / OSS(npm) / CLI / ★약 4.8k[^ccusage]
- 한줄 정의: 로컬 `~/.claude/projects/**/*.jsonl`을 파싱해 일/주/월/세션 토큰·비용 집계하는 CLI.
- 강점: **jsonl 로컬 파싱**(우리 FR-030/031과 동일 접근·검증), cache 토큰 분리 집계, 오프라인 가격 모드, `--instances`로 프로젝트별 그룹.
- 약점(우리 기회): **CLI 별도 실행**(관제탑에 미통합), 세션↔앱 실시간 매핑·UI 없음. → 우리는 파싱 로직 접근을 참고하되 **관제탑 내장·증분 파싱**(NFR-005)으로 흡수.

### 2-4. VT 렌더링 / .NET 컨트롤 substrate (Build vs Buy 근거)

#### [CS-011] XtermSharp
- URL: github.com/migueldeicaza/XtermSharp / OSS(.NET)[^xterm]
- 한줄 정의: .NET용 Xterm/VT100 에뮬레이터 **엔진**(프론트/백엔드 agnostic, `Feed(byte[])`로 데이터 주입).
- 강점: 파서/스크린 버퍼 엔진 성숙, 프론트엔드 교체 가능(FR-003/NFR-018 경계 분리와 정합).
- 약점: 제공 렌더 프론트엔드가 **Cocoa/Mac·Terminal.Gui(콘솔)뿐 — WPF 렌더러 없음**. 유지보수 활성도 낮음. → 공식 WT 렌더러를 임베드하는 채택안(EasyWindowsTerminalControl)이 우세. 미채택.

#### [CS-012] libvt100 / vtparse 계열
- URL: github.com/taterbase/libvt100 등 / OSS[^libvt100]
- 한줄 정의: 순수 C# VT100/ANSI **파서 라이브러리**(파싱만, 렌더는 호스트가 담당 — Paul Williams 상태머신 계보).
- 강점: 순수 파서 라이브러리로 경량·라이선스 명확.
- 약점: 파서만 제공(렌더·alt-screen은 호스트 자체 구현 필요) → 공식 WT 렌더러를 임베드하는 채택안(EasyWindowsTerminalControl) 대비 부담이 크다. 미채택.

#### [CS-013] Windows Terminal Control (embeddable)
- URL: devblogs "Building Windows Terminal with WinUI" / github.com/Corillian/WindowsTerminal[^winui][^embed]
- 한줄 정의: Windows Terminal 코어를 임베드하는 재사용 컨트롤(WinUI3).
- 강점: 성숙한 렌더·VT·탭을 통째로 재사용 가능(이론상 최단 경로).
- 약점: **WinUI3/UWP** — WPF 앱 직접 임베드 마찰(투명 합성 불가·Win10 제약·접근성 자체 구현), **앱-소유 세션 주입/제어 API 미노출**, 컨트롤 커스터마이즈 통제력 낮음. → WinUI3 직접 임베드로는 우리 관제탑 요구(임의 세션 주입 FR-014·상태 추적 FR-016·앱-소유 강제 FR-039)를 못 채움 → **공식 WT 렌더러를 WPF에 임베드하는 EasyWindowsTerminalControl 채택(BUY)**, self-build는 폴백(10 TS-03).

---

## 3. 경쟁·유사 솔루션 비교 (FR 기준)

범례: ● 완전 지원 / ◐ 부분·간접 / ○ 없음. 비교 대상은 whole-product 직접 경쟁 위주(터미널 substrate는 CS-001 대표).

| 기능 축 (FR) | 우리(예정) | CS-006 Squad | CS-007 Conductor | CS-005 Warp | CS-008 Vibe | CS-001 WinTerm |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| 앱-소유 세션 임베드·소유 (FR-001·039) | ● | ◐ | ◐ | ● | ◐ | ● |
| 다중 세션 탭·전환 (FR-006·017) | ● | ● | ● | ● | ● | ● |
| 의도별 세션 프로파일·프리셋 (FR-019·022) | ● | ◐ | ○ | ◐ | ○ | ◐ |
| 세션에 커맨드/트리거 주입 (FR-014·027) | ● | ◐ | ◐ | ● | ● | ○ |
| **세션 간 IPC 채널 협업 (FR-024~029)** ★ | ● | ○ | ○ | ◐ | ○ | ○ |
| 토큰 관측·집계 내장 (FR-030~032) | ● | ○ | ○ | ◐ | ○ | ○ |
| 프롬프트 자산(~/.claude) 편집 (FR-033~036) | ● | ○ | ○ | ○ | ○ | ○ |
| Windows 네이티브 데스크톱 (NFR-019) | ● | ● | ○ | ● | ● | ● |
| 로컬 전용·무인증·무료 (FR-038) | ● | ● | ● | ◐ | ● | ● |

관찰: **★행(세션 간 IPC 협업)·토큰 내장·자산 편집** 세 행에서 우리만 ●. 나머지 오케스트레이터는 전부 worktree 격리라 세션이 서로 대화하지 않는다. 이 세 행이 Control Tower의 방어 가능한 차별 묶음.

### 3-1. 포지셔닝 맵

```
                high  session lifecycle CONTROL-TOWER
                              |
       CONDUCTOR *   SQUAD *  |
       VIBE *        WARP *   |          * OURS(target)
                              |
   iso --------------------- +--------------------- ipc   (collaboration model)
                              |
       WINTERM *  VSCODE *    |
       TMUX *     WEZTERM *   |
                              |
                low  plain terminal / viewer
```

캡션: 가로축 = 세션 협업 모델(왼쪽 iso=worktree 격리 → 오른쪽 ipc=세션 간 실시간 IPC 채널 협업), 세로축 = 관제 범위(아래 plain terminal/뷰어 → 위 세션 lifecycle 관제탑). OURS는 **우상단(고관제 + 세션 간 IPC 협업) 단독 점유**를 목표로 한다. 오케스트레이터군(Squad/Conductor/Vibe/Warp)은 상단이되 좌측(격리)에 몰려 있어 오른쪽이 비어 있다.

---

## 4. Build vs Buy 결론

판단 축: **비용 · 출시 속도 · 통제력 · 락인/리스크**. 핵심 차별화 영역 → Build, 범용·비핵심 → Buy/Reuse.

| # | 영역 | 결정 | 근거(경쟁 벤치마크 + 브리프) | 관련 |
|---|---|---|---|---|
| B1 | 임베드 터미널 엔진(spawn·렌더·주입) | **INTEGRATE/BUY** (EasyWindowsTerminalControl, MIT) | 공식 Windows Terminal 렌더러(Microsoft.Terminal.Control/Wpf)를 WPF에 임베드하는 NuGet 컨트롤. 앱-소유·주입은 TermPTY API로 통제. WinUI3 직접 임베드의 마찰(투명 합성 불가·주입 API 미노출)은 백엔드만 WPF용으로 취해 회피 | FR-001·002·014·039 |
| B2 | VT 시퀀스 파싱 | **엔진 내장** | 공식 WT 렌더러가 파싱·렌더를 일체 제공 → 자체 파서 채택 불필요 | FR-003·NFR-018 |
| B3 | 셀 렌더러 | **엔진 내장**(공식 WT GPU 렌더러) | 자체 셀 렌더러 대신 공식 렌더러 신뢰. self-build(MS GUIConsole.ConPTY)는 폴백 | FR-004·005·007 |
| B4 | IPC 채널·relay·전송 | **REUSE**(skill_ipc_control) | CS군 어디도 세션 간 IPC 없음 = 재사용 자산이 곧 차별. 재구현 금지(NFR-017), GUI 프론트엔드만. 결정 #3 | FR-024·025·027·029 |
| B5 | 토큰 집계(jsonl) | **BUILD-light**(자체 증분 파서) | ccusage(CS-010)로 jsonl 접근 검증됨 — 로직 참고하되 CLI 종속 회피, 관제탑 내장·증분(NFR-005). 결정 #11 | FR-030·031·032 |
| B6 | 세션 오케스트레이션 UX(프로파일·다중세션·IPC 협업 뷰) | **BUILD**(차별화 핵심) | Squad/Conductor보다 깊은 프로파일 애그리거트 + 유일한 IPC 협업 뷰. 통제력·차별화 우선 | FR-011·019·022·026·028 |
| B7 | 프롬프트 자산 편집(~/.claude 트리·에디터) | **BUILD-light** | 경쟁 부재(공백), 범용 파일 트리+에디터라 경량 자체 구현. 경로 안전(NFR-008) | FR-033~036 |
| B8 | 배포·업데이트 | **BUY/Reuse**(ClickOnce) | 기존 파이프라인 계승, 출시 속도·비용. 결정 #8(브리프 §8) | FR-041·NFR-021 |

요약: **차별화 3축(세션 소유·IPC 협업·관측/자산 내장)은 Build, 터미널 렌더 엔진·relay·배포 등 범용은 Buy/Reuse.** 앱-소유·주입 통제는 자체 구현하되 파싱·렌더는 공식 WT 렌더러를 임베드한다. 리스크는 엔진 native 배포에 집중 → 10 참조.

---

## 5. 시사점 — 설계 반영점

### 5-1. 차별화 → FR/설계 결정 매핑

| 차별화 포인트 | 근거(경쟁 공백) | 반영 FR/설계 결정 |
|---|---|---|
| D1. **세션 간 IPC 채널 협업** | 전 오케스트레이터가 worktree 격리(세션이 대화 안 함) | FR-024~029 강화 — 채널 대화 뷰(FR-028)를 1급 화면으로. skill_ipc_control 재사용(NFR-017) |
| D2. **앱-소유 ConPTY + 임의 주입** | Squad는 tmux 위, Conductor는 spawn만. 임의 세션 주입 제어 드묾 | FR-001·014·027 — 입력 경로가 렌더와 독립이라 주입을 처음부터 제어 |
| D3. **의도별 세션 프로파일(재사용 애그리거트)** | Squad profile picker는 얕음, Conductor는 worktree 중심 | FR-019~023 — 6필드 프로파일 + as(IPC 식별자). 프리셋 즉시 기동 |
| D4. **Windows 네이티브 관제탑** | Conductor macOS 전용·Vibe 웹·Squad TUI | NFR-019·FR-040 — WPF Shell, 데스크톱 통합 관제 |
| D5. **토큰 관측 내장** | ccusage는 CLI 별도 | FR-030~032 — 세션↔jsonl 실시간 매핑·세션별 표시(ccusage 미충족 UX) |
| D6. **프롬프트 자산 편집 내장** | 경쟁 전무 | FR-033~036 — 관제 중 자산 편집(경로 안전 NFR-008) |
| D7. **로컬·무료·무인증** | Warp 유료·클라우드 논란 | FR-038·NFR-006 — 포트 0, BYO 구독. 프라이버시 포지셔닝 |

### 5-2. 참고할 UX / 회피할 함정

- 참고: Conductor "**3~5 병렬 sweet spot**" → NFR-012 상한은 8이나 목록/탭 UX는 3~5 가독 최적화. Warp의 **리뷰 루프·세로 탭 메타**(branch/PR) → 세션 목록 메타 표시 참고. WezTerm **렌더 성능**을 NFR-001 상향 레퍼런스로.
- 회피할 함정: **ConPTY 뷰포트 소유·리페인트 폭주**(VS Code 문서화) → 임베드 터미널 엔진이 대량 리페인트를 GPU 렌더러로 흡수(NFR-001, FR-005 alt-screen). **각 파이프 별도 스레드 서비스**(ConPTY 데드락 경고) → I/O 스레딩 설계 반영. **WinUI3 직접 임베드 마찰** → 공식 WT 렌더러를 WPF로 임베드(EasyWindowsTerminalControl 채택), self-build는 폴백.

### 5-3. Impact vs Effort 우선순위 (deep)

```
        high IMPACT
             |
  D5 token   |  D1 IPC-collab
  D3 profile |  D2 conpty-embed
  -----------+------------------  EFFORT
  D6 asset   |  render-2 altscreen
  D7 local   |  pane-split / restore
             |
        low IMPACT
```

캡션: 세로축 Impact, 가로축 Effort(왼쪽 Low·오른쪽 High). 사분면 해석:
- **Big Bets(고Impact·고Effort)**: D1 세션 간 IPC 협업 · D2 ConPTY 임베드+렌더 → v1 핵심 투자, PoC 선행(결정 B2·B3).
- **Quick Wins(고Impact·저Effort)**: D5 토큰 내장(ccusage 로직 참고) · D3 세션 프로파일 프리셋 → 초기 마일스톤에서 차별화 조기 가시화.
- **Fill-ins(저Impact·저Effort)**: D6 자산 편집 · D7 로컬 포지셔닝 → 여력 시.
- **Defer(저Impact·고Effort)**: alt-screen 완전 렌더(②단계)·pane 분할(FR-008)·상태 완전 복원(FR-043) → 후속 마일스톤(브리프 결정 #5/#8과 정합).

---

## 6. 출처 (URL)

[^gartner]: Gartner — Enterprise AI Coding Agents: 2026 Market Guide & Trends. https://www.gartner.com/en/articles/enterprise-ai-coding-agent-market
[^anthropic]: Anthropic — 2026 Agentic Coding Trends Report. https://resources.anthropic.com/hubfs/2026%20Agentic%20Coding%20Trends%20Report.pdf
[^squad]: Claude Squad. https://github.com/smtg-ai/claude-squad
[^agentteams]: Claude Code Docs — Orchestrate teams of Claude Code sessions (Agent Teams). https://code.claude.com/docs/en/agent-teams
[^conductor]: Conductor. https://www.conductor.build/
[^conductor-hn]: Show HN — Conductor. https://news.ycombinator.com/item?id=44594584
[^vibe]: Vibe Kanban. https://github.com/BloopAI/vibe-kanban
[^ccusage]: ccusage. https://github.com/ryoppippi/ccusage
[^warp-agents]: Warp Agents. https://www.warp.dev/agents
[^warp-multi]: Warp Docs — Run multiple AI coding agents. https://docs.warp.dev/guides/agent-workflows/how-to-run-multiple-ai-coding-agents/
[^warp200]: Implicator — Warp turns the terminal into an agent platform at $200/mo. https://www.implicator.ai/warp-turns-the-terminal-into-an-agent-platform-at-200-a-month/
[^wezterm]: WezTerm Features. https://wezterm.org/features.html
[^vscode]: VS Code — Terminal Advanced (xterm.js / ConPTY). https://code.visualstudio.com/docs/terminal/advanced
[^winui]: DevBlogs — Building Windows Terminal with WinUI. https://devblogs.microsoft.com/commandline/building-windows-terminal-with-winui/
[^embed]: Corillian/WindowsTerminal (embeddable WinUI3) + Terminal repo. https://github.com/Corillian/WindowsTerminal · https://github.com/microsoft/terminal
[^xterm]: XtermSharp. https://github.com/migueldeicaza/XtermSharp
[^libvt100]: libvt100 (pure C# VT100/ANSI parser). https://github.com/taterbase/libvt100

기타 참고:
- ConPTY 소개/샘플: https://devblogs.microsoft.com/commandline/windows-command-line-introducing-the-windows-pseudo-console-conpty/ · https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session · https://github.com/microsoft/terminal/blob/main/samples/ConPTY/GUIConsole/GUIConsole.ConPTY/PseudoConsole.cs
- Nimbalyst — Best agent management tools 2026(오케스트레이터 교차검증). https://nimbalyst.com/blog/best-agent-management-tools-2026/

---

## 7. 미확인·후속 조사 항목

1. **국내 유사 서비스 부재**: 한국어/국내 "세션 관제탑" 직접 경쟁 검색 미발견 → 해외 오픈소스로 대체 조사(재검색 시 도메인 확장 권고).
2. **세그먼트 직접 시장 수치 없음**: §1-1 수치는 엔터프라이즈 코딩 에이전트 전반. "로컬·단일 파워유저 관제탑" 직접 TAM 미확보.
3. **Warp 세션 협업 깊이 미확정**: "공유·관찰"이 우리 IPC 메시징과 동급인지(진짜 세션 간 통신인지) 문서상 불명 → D1 차별성 재확인 필요.
4. **VT 파서 후보 실측 미확보**: vtnetcore vs libvt100(CS-012) vs XtermSharp engine(CS-011)의 성능·커버리지·라이선스 실측 → 10 기술리서처 숙제②로 이관.
5. **경쟁 사용자 규모 정밀치 부재**: Squad ★7.9k·ccusage ★4.8k는 GitHub stars일 뿐 MAU 아님. Conductor MAU 비공개.
6. **Claude Code 네이티브 Agent Teams 확장 위협**: 공식 팀 기능이 GUI/관측까지 확장 시 서드파티 잠식 가능 → 로드맵(12)에서 방어 포지셔닝 모니터링.
7. **ccusage 라이선스·증분 구현 세부** 미확인(내장 흡수 시 로직 참조 범위 확정 필요).

---

## 8. 문서 메타

- 버전: v1.0 / 생성일: 2026-07-01
- 담당: plan_competitor_researcher · 깊이: deep · 조사 13개(CS-001~013, 국내 0/해외 13)
- 입력: `00_meeting_brief.md`(정체성·ConPTY 결정) · `04_requirements.md`(FR/NFR 비교 축, 참조 전용)
- 관련 문서: [`04_requirements`](./04_requirements.md)(FR 비교 축) · [`10_tech`](./10_tech.md)(터미널 엔진·RISK) · [`12_roadmap`](./12_roadmap.md)(레이어 우선순위)
- 미해결·후속: §7 항목 → `13_followups`로 이관(특히 파서 실측·Warp 협업 깊이·Agent Teams 위협).
- 비고: 본 문서는 레지스트리 네임스페이스 ID를 발번하지 않는다(CS-###는 문서 로컬 라벨). FR은 참조 전용.
