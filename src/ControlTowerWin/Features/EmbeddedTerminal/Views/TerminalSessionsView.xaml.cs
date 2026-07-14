using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ControlTowerWin.Features.EmbeddedTerminal.Interfaces;
using ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

namespace ControlTowerWin.Features.EmbeddedTerminal.Views;

public partial class TerminalSessionsView : UserControl
{
    public TerminalSessionsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /* VM의 커맨드 바 포커스 요청("명령 실행" 컨텍스트 메뉴)을 View가 처리 */
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is TerminalSessionsViewModel vm)
        {
            vm.FocusCommandBarRequested += () => CommandBox.Focus();
        }
    }

    /* 커맨드 바 Enter → 주입 실행 */
    private void CommandBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is TerminalSessionsViewModel vm && vm.InjectCommand.CanExecute(null))
        {
            vm.InjectCommand.Execute(null);
            e.Handled = true;
        }
    }

    /* TreeView.SelectedItem은 읽기전용이라 바인딩 불가 → 코드비하인드에서 VM 선택 상태로 반영.
       선택 노드(탭/터미널)를 SelectedNode에 실어 rename 대상으로 삼는다. */
    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not TerminalSessionsViewModel vm) return;

        vm.SelectedNode = e.NewValue as IRenamableNode;

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

    /* 우클릭 시 대상 노드를 먼저 선택 → 컨텍스트 메뉴(SC-23)가 그 노드에 작용 */
    private void OnTreeRightClick(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (item != null) item.IsSelected = true;
    }

    /* pane(터미널) 클릭·포커스 시 → 해당 터미널을 좌측 트리에서 선택.
       트리 선택이 OnTreeSelectionChanged를 통해 SelectedTerminal·pane 테두리·트리 하이라이트를 일괄 동기. */
    private void Pane_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TerminalViewModel terminal)
        {
            SelectTerminalInTree(terminal);
        }
    }

    /* 터미널에 해당하는 TreeViewItem을 프로그램적으로 선택(포커스는 터미널에 유지) */
    private void SelectTerminalInTree(TerminalViewModel terminal)
    {
        if (DataContext is not TerminalSessionsViewModel vm) return;
        var tab = vm.Tabs.FirstOrDefault(t => t.Terminals.Contains(terminal));
        if (tab is null) return;
        if (SessionTree.ItemContainerGenerator.ContainerFromItem(tab) is not TreeViewItem tabItem) return;
        tabItem.IsExpanded = true;
        if (tabItem.ItemContainerGenerator.ContainerFromItem(terminal) is TreeViewItem termItem
            && !termItem.IsSelected)
        {
            termItem.IsSelected = true;
        }
    }

    /* F2 → 선택 노드 인라인 편집 시작 */
    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F2) return;
        if (DataContext is TerminalSessionsViewModel vm && vm.SelectedNode is IRenamableNode node)
        {
            node.IsEditing = true;
            e.Handled = true;
        }
    }

    /* 라벨 더블클릭 → 인라인 편집 시작 (단일 클릭 선택/확장과 구분) */
    private void Label_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement fe && fe.DataContext is IRenamableNode node)
        {
            node.IsEditing = true;
            e.Handled = true;
        }
    }

    /* TextBox가 편집 모드로 보이게 되는 순간 포커스 + 전체 선택.
       (Loaded는 최초 1회만 발화 → 재편집 대응 위해 IsVisibleChanged 사용) */
    private void RenameBox_VisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.IsVisible)
        {
            tb.Dispatcher.BeginInvoke(new Action(() =>
            {
                tb.Focus();
                tb.SelectAll();
            }), DispatcherPriority.Input);
        }
    }

    /* Enter=커밋, Esc=취소 */
    private void RenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (e.Key == Key.Enter)
        {
            CommitRename(tb);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelRename(tb);
            e.Handled = true;
        }
    }

    /* 포커스 이탈 시 커밋 */
    private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb) CommitRename(tb);
    }

    /* 편집값을 Title에 반영(빈값은 setter가 거부) 후 편집 종료 */
    private static void CommitRename(TextBox tb)
    {
        tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (tb.DataContext is IRenamableNode node) node.IsEditing = false;
    }

    /* 원복 후 편집 종료 (이후 LostFocus 커밋을 무해화) */
    private static void CancelRename(TextBox tb)
    {
        if (tb.DataContext is IRenamableNode node)
        {
            tb.Text = node.Title;
            node.IsEditing = false;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null && current is not T)
        {
            current = VisualTreeHelper.GetParent(current);
        }
        return current as T;
    }
}
