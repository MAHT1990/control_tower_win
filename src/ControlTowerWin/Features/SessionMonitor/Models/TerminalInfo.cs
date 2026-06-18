namespace ControlTowerWin.Features.SessionMonitor.Models;
public class TerminalInfo
{
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName => $"[{ProcessName}] PID {Pid}";
}
