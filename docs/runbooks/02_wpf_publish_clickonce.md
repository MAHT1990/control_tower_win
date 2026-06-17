# Runbook 02 — WPF 앱 배포 (ClickOnce)

## 목표
Visual Studio의 Publish 도구로 ClickOnce 설치 파일을 생성한다.
사용자는 `setup.exe` 실행 → 설치 → 시작 메뉴에서 앱을 실행하는 전통적인 Windows 설치 경험을 갖는다.

---

## 개념

### .NET 5 이상은 Publish 도구 사용
.NET 10은 기존 Publish Wizard 방식이 아닌 **Publish 도구 + 프로파일(.pubxml)** 방식을 사용한다.
마법사 UI는 비슷하지만 내부 단계 구성이 다르다.

### Self-contained 배포란?
.NET 런타임을 앱에 통째로 포함시키는 배포 방식이다.
대상 PC에 .NET이 설치되어 있지 않아도 `setup.exe` 하나로 실행된다.

```
Self-contained 빌드 과정
  내 코드 (.dll)
     +
  .NET 런타임 패키지 (win-x64)  ◀── NuGet에서 다운로드
     ↓
  HelloWorldWpf.exe (런타임 포함 실행파일)
```

### NuGet이란?
NuGet은 .NET의 **패키지 저장소 시스템**이다.
npm(Node.js), pip(Python)과 동일한 개념으로, 외부 라이브러리나 런타임 패키지를 받아오는 곳이다.

```
패키지 소스 종류
  ┌──────────────────────────────────────────────────────┐
  │ Microsoft Visual Studio Offline Packages             │
  │   → VS 설치 시 로컬에 포함된 패키지만               │
  │   → win-x64 런타임 없음 (용량 문제로 제외)          │
  ├──────────────────────────────────────────────────────┤
  │ nuget.org  (https://api.nuget.org/v3/index.json)    │
  │   → 인터넷 공식 저장소, 모든 패키지 포함 ✅         │
  └──────────────────────────────────────────────────────┘
```

VS 기본 설정이 오프라인 패키지만 바라보고 있으면
Self-contained 빌드에 필요한 `win-x64` 런타임을 찾지 못해 게시가 실패한다.

### UNC 경로란?
UNC(Universal Naming Convention)는 **네트워크 자원을 가리키는 표준 경로 형식**이다.

```
\\서버이름\공유폴더\하위경로
  ↑ 항상 \\로 시작
```

ClickOnce가 설치 위치에 UNC를 요구하는 이유:
- 설치 위치는 "다른 PC에서도 접근 가능한 경로"여야 배포가 성립되기 때문
- 로컬 경로(`C:\`)는 배포받는 사람의 PC에서는 의미가 없음
- `\\localhost\...`는 "이 PC 자체를 서버로 간주"하는 UNC 표현 → 로컬 배포에 사용 가능

```
변환 규칙
  일반 경로: C:\Users\kitor\Projects\control_tower_win\publish
  UNC 경로:  \\localhost\Users\kitor\Projects\control_tower_win\publish

  C:\ 제거 후 앞에 \\localhost\ 붙이기
```

---

## 사전 조건

- [ ] Runbook 01 완료 (WPF 프로젝트 정상 빌드 확인)
- [ ] nuget.org 패키지 소스 등록 (아래 참고, 1회만)

### nuget.org 소스 등록 방법

1. VS 상단 메뉴 → **도구 → NuGet 패키지 관리자 → 패키지 관리자 설정**
2. 좌측 **패키지 소스** 클릭
3. 우상단 **`+`** 버튼 클릭
4. 아래 정보 입력 후 **업데이트** → **확인**

| 항목 | 값 |
|------|-----|
| 이름 | `nuget.org` |
| 소스 | `https://api.nuget.org/v3/index.json` |

---

## Step 1. Publish 도구 열기

솔루션 탐색기 → **HelloWorldWpf 프로젝트** 우클릭 → **게시(Publish)**

- 처음이면 프로파일 선택 화면 → **새로 만들기(New)** 클릭
- 기존 프로파일 삭제 후 재시작하려면: 프로파일 옆 **`...`** → **프로파일 삭제** → **새로 만들기**

---

## Step 2. 게시 대상: 폴더 선택

**폴더(Folder)** 선택 → **다음**

---

## Step 3. 특정 대상: ClickOnce 선택

**ClickOnce** 선택 → **다음**

---

## Step 4. 게시 위치 지정

설치 파일이 생성될 경로 입력:

```
C:\Users\kitor\Projects\control_tower_win\publish
```

**다음** 클릭

---

## Step 5. 설치 위치 선택

**로컬 폴더 또는 파일 공유** 선택 후 UNC 경로 입력:

```
\\localhost\Users\kitor\Projects\control_tower_win\publish
```

> ⚠️ `C:\Users\...` 형식 입력 시 오류 발생 — 반드시 `\\localhost\...` 형식 사용

**다음** 클릭

---

## Step 6. 설정(Settings) 구성

| 항목 | 설정값 |
|------|--------|
| 오프라인 가용성 | **오프라인에서도 사용 가능** 체크 ✅ (시작 메뉴 바로가기 생성) |
| 버전 자동 증가 | **게시할 때마다 자동 증가** 체크 권장 ✅ |
| 필수 구성 요소 | **필수 구성 요소** 링크 클릭 → **.NET 10 Desktop Runtime** 체크 |

**다음** 클릭

---

## Step 7. 매니페스트 서명

개인 배포 시: **서명 안 함** 선택 (또는 기본값 유지)

> 설치 시 "알 수 없는 게시자" 보안 경고가 뜨지만 무시하고 설치 가능

**다음** 클릭

---

## Step 8. 구성(Configuration)

| 항목 | 설정값 |
|------|--------|
| 구성 | Release |
| 배포 모드 | Self-contained |
| 대상 런타임 | win-x64 |

**다음** 클릭

---

## Step 9. 프로파일 저장 및 게시

**마침(Finish)** → 프로파일(.pubxml) 저장

요약 페이지에서 → **게시(Publish)** 클릭

완료 후 결과물:

```
publish/
 ├─ setup.exe                 ← 사용자에게 전달하는 설치 파일
 ├─ HelloWorldWpf.application
 └─ Application Files/
```

---

## Step 10. 설치 테스트

1. `publish/setup.exe` 실행
2. 보안 경고 → **설치** 클릭
3. 시작 메뉴에서 **HelloWorldWpf** 검색 후 실행
4. "Hello World" 창 출력 확인
5. **설정 → 앱 및 기능** 에서 제거 가능한지 확인

---

## 체크리스트

- [ ] 사전 조건: nuget.org 소스 등록 확인
- [ ] Step 1: Publish 도구 열기
- [ ] Step 2: 폴더 선택
- [ ] Step 3: ClickOnce 선택
- [ ] Step 4: 게시 위치 지정
- [ ] Step 5: UNC 경로로 설치 위치 입력
- [ ] Step 6: 오프라인 가용성 + 필수 구성 요소 설정
- [ ] Step 7: 서명 안 함 선택
- [ ] Step 8: Release + Self-contained + win-x64 설정
- [ ] Step 9: 게시 완료, setup.exe 생성 확인
- [ ] Step 10: 설치 → 시작 메뉴 실행 → "Hello World" 출력 확인

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| 게시 메뉴가 없음 | 프로젝트가 아닌 솔루션 우클릭 | 솔루션 탐색기에서 **프로젝트** 우클릭 |
| ClickOnce 옵션이 없음 | Step 2에서 폴더를 선택하지 않음 | 폴더 → ClickOnce 순서로 선택 |
| "정규화된 UNC 경로여야 합니다" | 설치 위치에 `C:\` 형식 입력 | `\\localhost\...` 형식으로 변환 |
| 게시 실패: `win-x64 패키지를 찾을 수 없습니다` | nuget.org 소스 미등록 | 사전 조건: nuget.org 소스 추가 |
| 설치 후 시작 메뉴에 없음 | Step 6 오프라인 가용성 미체크 | 프로파일 수정 후 재게시 |
| 실행 시 ".NET 없음" 오류 | Self-contained 미설정 | Step 8 Self-contained 확인 |
| "알 수 없는 게시자" 경고 | 앱 서명 없음 | 개인 배포 시 무시 가능 |

---

## 참고

- [ClickOnce for .NET 5 and later](https://learn.microsoft.com/en-us/visualstudio/deployment/clickonce-deployment-dotnet?view=vs-2022)
- [Deploy a .NET Windows Desktop app with ClickOnce](https://learn.microsoft.com/en-us/visualstudio/deployment/quickstart-deploy-using-clickonce-folder?view=vs-2022)
