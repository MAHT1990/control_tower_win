using System.Windows.Input;
using ControlTowerWin.Features.TerminalLauncher.Services;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.TerminalLauncher.ViewModels;

public class NewTerminalViewModel : ViewModelBase
{
    public ICommand OpenCommand { get; }

    // 클래스명 TerminalLauncher가 폴더 네임스페이스와 같아 한정 필요 (Services. 접두)
    public NewTerminalViewModel(Services.TerminalLauncher launcher) =>
        OpenCommand = new RelayCommand(_ => launcher.OpenNewWindow());
}