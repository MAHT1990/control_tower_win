namespace ControlTowerWin.Features.EmbeddedTerminal.Interfaces;

/// <summary>
/// 임베드 터미널의 엔진 경계 계약 (NFR-018). 주입/캡처를 이 1곳으로 격리해
/// 엔진(EasyTerminalControl)↔폴백 교체 시 상위 소비자(ViewModel)가 무변경이 되게 한다.
/// 수명(Close/Restart)은 Runbook 09에서 확장한다.
/// </summary>
public interface ITerminalSession
{
    /* ConPTY가 기동되어 주입/캡처가 가능한 상태인지 */
    bool IsReady { get; }

    /* 입력 파이프에 텍스트 주입(FR-014). 개행(실행)은 호출자가 포함한다. */
    void Send(string text);

    /* 현재 콘솔 출력 캡처(FR-002). 세션 간 출력 라우팅(FR-047, Task 4)의 소스. */
    string GetOutputText();

    /* 세션 재시작(FN-SES-10). 기존 ConPTY를 정리하고 새 프로세스를 기동한다. */
    void Restart();

    /* 세션 종료·정리(FN-TRM-02/FN-SES-05). ConPTY 자식을 정리해 좀비를 방지한다(멱등). */
    void Close();
}
