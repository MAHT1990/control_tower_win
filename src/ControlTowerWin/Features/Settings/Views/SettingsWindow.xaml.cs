using System.Windows;
using ControlTowerWin.Features.Settings.ViewModels;

namespace ControlTowerWin.Features.Settings.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        /* 저장 성공(방송 발생) 시 창 닫기 — 취소는 IsCancel 버튼이 처리 */
        viewModel.FontSettingsChanged += (_, _) => Close();
    }
}
