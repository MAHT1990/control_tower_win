using System;
using ControlTowerWin.Features.EmbeddedTerminal.Interfaces;
using EasyWindowsTerminalControl;

namespace ControlTowerWin.Features.EmbeddedTerminal.Services;

/// <summary>
/// EasyTerminalControl을 감싸 ITerminalSession(주입/캡처)을 실현하는 엔진 경계 구현.
/// 주입=ConPTYTerm.WriteToTerm, 캡처=LogConPTYOutput + GetConsoleText(on-demand).
/// ConPTYTerm은 프로세스 기동 전 null일 수 있어 매 호출 시 지연 접근한다.
/// </summary>
public class EasyTerminalSession : ITerminalSession
{
    private readonly EasyTerminalControl _control;
    private bool _closed;

    public EasyTerminalSession(EasyTerminalControl control)
    {
        _control = control;
        /* 출력 누적 로깅을 세션 생성 시 조기 활성화 → 이후 GetConsoleText가 누적분을 반환.
           캡처 시점에 켜면 그 전 출력을 놓치므로 여기서 미리 켠다(FR-047 캡처 신뢰성). */
        _control.LogConPTYOutput = true;
    }

    public bool IsReady => _control.ConPTYTerm != null;

    /* 입력 파이프에 주입(사람 타이핑과 한 경로에 합류, NFR-002) */
    public void Send(string text)
    {
        var term = _control.ConPTYTerm;
        if (term is null || string.IsNullOrEmpty(text)) return;
        term.WriteToTerm(text.AsSpan());
    }

    /* 현재까지 누적된 콘솔 텍스트 캡처(관찰 전용, VT 변형 없음). */
    public string GetOutputText() => _control.ConPTYTerm?.GetConsoleText() ?? string.Empty;

    /* 깨끗한 새 term으로 재시작(기존 dispose). StartupCommandLine 재적용. */
    public void Restart() => _control.RestartTerm();

    /* ConPTY 프론트엔드 분리·정리(좀비 방지). 멱등 — 중복 호출 무해. */
    public void Close()
    {
        if (_closed) return;
        _closed = true;
        _control.DisconnectConPTYTerm();
    }
}
