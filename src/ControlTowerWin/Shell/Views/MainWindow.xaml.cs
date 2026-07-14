using System.ComponentModel;
using System.Windows;
using ControlTowerWin.Shell.ViewModels;

namespace ControlTowerWin.Shell.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        Closing += OnClosing;
    }

    /* 앱 종료 시 앱-소유 ConPTY 세션 일괄 정리(FN-TRM-02, 좀비 방지) */
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.Sessions.CleanupAll();
        }
    }
}