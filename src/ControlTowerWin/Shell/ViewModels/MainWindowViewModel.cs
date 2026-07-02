using ControlTowerWin.Features.EmbeddedTerminal.ViewModels;
using ControlTowerWin.Features.SessionMonitor.Services;
using ControlTowerWin.Features.SessionMonitor.ViewModels;
using ControlTowerWin.Features.TerminalLauncher.Services;
using ControlTowerWin.Features.TerminalLauncher.ViewModels;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Shell.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public SessionListViewModel SessionList { get; }
    public NewTerminalViewModel NewTerminal { get; }
    public TerminalViewModel Terminal { get; }

    public MainWindowViewModel()
    {
        SessionList = new SessionListViewModel(new ProcessTracker());
        NewTerminal = new NewTerminalViewModel(new TerminalLauncher());
        Terminal = new TerminalViewModel();
    }
}