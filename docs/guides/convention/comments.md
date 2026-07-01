# 주석 컨벤션

## 개요

ControlTowerWin의 **주석 작성 규칙**이다. 주석 스타일을 한 가지로 통일해, 어떤 파일을 열어도
주석의 모양·위치가 예측 가능하게 한다.

- 적용 대상: 모든 C# 소스(`*.cs`)
- 핵심: C# 주석은 `/* */` 하나로, XML 문서 주석은 `///` 하나로 통일. **줄 끝(인라인) 주석 금지.**

---

## 규칙

### 1. C# 주석 — 한 줄이든 여러 줄이든 `/* */`

`//` 를 쓰지 않는다. 한 줄짜리도 `/* */` 로 감싼다.

```csharp
/* 한 줄 주석도 이렇게 */

/* 여러 줄도
   같은 기호로
   감싼다 */
```

### 2. 코드와 같은 줄에 주석 금지

주석은 **항상 자기 줄**에 둔다. 보통 설명 대상 코드의 **바로 위 줄**에 쓴다.

```csharp
/* wt는 Store 앱 별칭 → 반드시 true */
UseShellExecute = true
```

### 3. XML 문서 주석 — 한 줄이든 여러 줄이든 `///`

타입·멤버 문서화(IntelliSense·문서 생성)는 `///` 로 통일한다.

```csharp
/// <summary>
/// 활성 PowerShell 세션을 추적한다.
/// </summary>
public class ProcessTracker { }
```

---

## Good / Bad

**Good** — `/* */`, 자기 줄, 대상 위
```csharp
/* 강한 참조: GC가 Exited 막는 것을 방지 */
private readonly Dictionary<int, Process> _tracked = new();
```

**Bad** — `//` 사용
```csharp
// 강한 참조: GC가 Exited 막는 것을 방지
private readonly Dictionary<int, Process> _tracked = new();
```

**Bad** — 코드와 같은 줄(인라인)
```csharp
private readonly Dictionary<int, Process> _tracked = new();   // 강한 참조
```

**Bad** — XML 문서 주석에 `/* */` 또는 `//`
```csharp
/* <summary>설명</summary> */          // 문서 주석은 /// 로
```

---

## 주의사항

1. **`//` 전면 금지**: 한 줄 주석이라도 `//` 대신 `/* */`. 일관성 유지.
2. **인라인 금지**: 코드 오른쪽에 주석을 붙이지 않는다. 대상 코드 위 별도 줄로 올린다.
3. **문서 주석은 예외적으로 `///`**: 타입/멤버 설명은 `///`(XML 문서 주석)로. 본문 주석(`/* */`)과 역할이 다르다.
4. **XAML 주석은 대상 외**: `*.xaml`은 `<!-- -->`만 가능하므로 본 규칙(`/* */`)을 적용하지 않는다. XAML도 인라인은 지양.
5. **자동 생성 템플릿 정리**: `AssemblyInfo.cs` 등 SDK가 만든 `//` 주석도 이 규칙으로 정리한다.
