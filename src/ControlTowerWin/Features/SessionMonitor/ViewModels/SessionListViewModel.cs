using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using ControlTowerWin.Features.SessionMonitor.Models;
using ControlTowerWin.Features.SessionMonitor.Services;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.SessionMonitor.ViewModels;

public class SessionListViewModel : ViewModelBase
{
    public ObservableCollection<TerminalInfo> Sessions { get; } = new();
    private readonly Dispatcher _ui = Application.Current.Dispatcher;

    public SessionListViewModel(ProcessTracker tracker)
    {
        tracker.SessionAdded += info => _ui.Invoke(() => Sessions.Add(info));
        tracker.SessionRemoved += pid => _ui.Invoke(() =>
        {
            var item = Sessions.FirstOrDefault(s => s.Pid == pid);
            if (item != null) Sessions.Remove(item);
        });
        tracker.Start();
    }
}