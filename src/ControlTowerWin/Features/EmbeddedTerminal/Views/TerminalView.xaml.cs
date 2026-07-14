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
        Unloaded += OnUnloaded;
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

    /* View가 트리에서 제거될 때(터미널 종료·앱 종료) ConPTY 정리(좀비 방지).
       keep-alive는 Collapsed일 뿐 언로드가 아니므로, 탭 전환에는 발화하지 않는다. */
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TerminalViewModel vm)
        {
            vm.Cleanup();
        }
    }
}
