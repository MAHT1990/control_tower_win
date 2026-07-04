using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

namespace ControlTowerWin.Features.EmbeddedTerminal.Views;

public partial class TerminalSessionsView : UserControl
{
    public TerminalSessionsView() => InitializeComponent();

    /* TreeView.SelectedItem은 읽기전용이라 바인딩 불가 → 코드비하인드에서 VM 선택 상태로 반영 */
    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not TerminalSessionsViewModel vm) return;

        switch (e.NewValue)
        {
            case TabViewModel tab:
                vm.SelectedTab = tab;
                break;
            case TerminalViewModel terminal:
                var parent = vm.Tabs.FirstOrDefault(t => t.Terminals.Contains(terminal));
                if (parent is not null)
                {
                    vm.SelectedTab = parent;
                    parent.SelectedTerminal = terminal;
                }
                break;
        }
    }
}