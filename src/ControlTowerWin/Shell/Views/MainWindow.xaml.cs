using System.ComponentModel;
using System.Windows;
using ControlTowerWin.Features.Settings.Views;
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

    /* 설정 창(SC-02) 모달 오픈 — airspace 제약상 터미널 존 밖 별도 창 */
    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var window = new SettingsWindow(vm.CreateSettingsViewModel()) { Owner = this };
        window.ShowDialog();
    }
}