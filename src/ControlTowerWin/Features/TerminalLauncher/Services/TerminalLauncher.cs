using System.Diagnostics;

namespace ControlTowerWin.Features.TerminalLauncher.Services;

public class TerminalLauncher
{
    public void OpenNewWindow() =>
        Process.Start(new ProcessStartInfo
        {
            FileName = "wt",
            Arguments = "-w -1 nt",
            /* wt는 Store 앱 별칭 → 반드시 true */
            UseShellExecute = true
        });
}