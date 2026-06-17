using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace HelloWorldWpf
{
    public class TerminalInfo
    {
        public int Pid { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string DisplayName => $"[{ProcessName}] PID {Pid}";
    }
    public partial class MainWindow : Window
    {
        public ObservableCollection<TerminalInfo> Terminals { get; } = new();

        private readonly DispatcherTimer _timer;
        private readonly Dictionary<int, Process> _trackedProcesses = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        private static readonly string[] PowerShellProcessNames = ["powershell", "pwsh"];

        private void OnTimerTick(object? sender, EventArgs e)
        {
            var current = PowerShellProcessNames
                .SelectMany(name => Process.GetProcessesByName(name))
                .ToArray();
            var currentPids = current.Select(p => p.Id).ToHashSet();

            foreach (var process in current)
            {
                if (!_trackedProcesses.ContainsKey(process.Id))
                {
                    _trackedProcesses[process.Id] = process;
                    Terminals.Add(new TerminalInfo { Pid = process.Id, ProcessName = process.ProcessName });

                    try
                    {
                        process.EnableRaisingEvents = true;
                        process.Exited += (s, _) =>
                        {
                            var pid = ((Process)s!).Id;
                            Dispatcher.Invoke(() => RemoveTerminal(pid));
                        };
                    }
                    catch { /* 권한 부족 프로세스는 폴링으로 감지 */ }
                }
            }

            // 폴링 기반 정리 (Exited 미발화 대비 이중 안전망)
            var gone = _trackedProcesses.Keys.Except(currentPids).ToList();
            foreach (var pid in gone)
                RemoveTerminal(pid);
        }

        private void RemoveTerminal(int pid)
        {
            _trackedProcesses.Remove(pid);
            var item = Terminals.FirstOrDefault(t => t.Pid == pid);
            if (item != null) Terminals.Remove(item);
        }

        private void OnNewTerminalClick(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "wt",
                Arguments = "-w -1 nt",
                UseShellExecute = true
            });
        }
    }
}