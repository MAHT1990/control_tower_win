using System.Windows;
using ControlTowerWin.Shell.ViewModels;

namespace ControlTowerWin.Shell.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}