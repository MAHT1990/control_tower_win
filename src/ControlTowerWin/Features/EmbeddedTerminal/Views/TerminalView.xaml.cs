using System.Windows;
using System.Windows.Controls;
using ControlTowerWin.Features.EmbeddedTerminal.Services;
using ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

namespace ControlTowerWin.Features.EmbeddedTerminal.Views;

public partial class TerminalView : UserControl
{
    public TerminalView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /* View 로드 시 EasyTerminalControl을 감싼 세션을 VM에 주입(엔진 경계 연결).
       ConPTYTerm은 지연 접근이므로 여기서 컨트롤 참조만 넘겨도 충분하다. */
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TerminalViewModel vm)
        {
            vm.AttachSession(new EasyTerminalSession(Term));
        }
    }
}
