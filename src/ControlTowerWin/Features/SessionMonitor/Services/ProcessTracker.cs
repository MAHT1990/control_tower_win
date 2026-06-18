using System.Diagnostics;
using System.Diagnostics.Tracing;
using ControlTowerWin.Features.SessionMonitor.Models;

namespace ControlTowerWin.Features.SessionMonitor.Services;

public class ProcessTracker
{
    private static readonly string[] Names = ["powershell", "pwsh"];

    // 강한 참조: GC가 Exited 막는 것을 방지
    private readonly Dictionary<int, Process> _tracked = new();
    private readonly System.Timers.Timer _timer = new(1000);
    private readonly object _lock = new();

    public event Action<TerminalInfo>? SessionAdded;
    public event Action<int>? SessionRemoved;

    public ProcessTracker() => _timer.Elapsed += (_, _) => Poll();

    public void Start() { Poll(); _timer.Start(); }
    public void Stop() => _timer.Stop();

    private void Poll()
    {
        var current = Names.SelectMany(Process.GetProcessesByName).ToArray();
        var currentPids = current.Select(p => p.Id).ToHashSet();

        lock (_lock)
        {
            foreach (var p in current)
            {
                if (_tracked.ContainsKey(p.Id)) continue;
                _tracked[p.Id] = p;
                SessionAdded?.Invoke(new TerminalInfo { Pid = p.Id, ProcessName = p.ProcessName });
                try
                {
                    p.EnableRaisingEvents = true;
                    p.Exited += (s, _) => Remove(((Process)s!).Id);
                }
                catch { /* 권한 부족 프로세스는 폴링으로 정리 */}
            }

            foreach (var pid in _tracked.Keys.Except(currentPids).ToList())
                Remove(pid);
        }
    }

    private void Remove(int pid)
    {
        lock (_lock)
            if (!_tracked.Remove(pid)) return;
        SessionRemoved?.Invoke(pid);
    }
}
