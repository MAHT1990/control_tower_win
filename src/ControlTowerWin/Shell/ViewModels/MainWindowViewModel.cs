using ControlTowerWin.Features.EmbeddedTerminal.ViewModels;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Shell.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public TerminalSessionsViewModel Sessions { get; }

    public MainWindowViewModel() => Sessions = new TerminalSessionsViewModel();
}