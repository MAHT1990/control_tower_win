using ControlTowerWin.Features.EmbeddedTerminal.ViewModels;
using ControlTowerWin.Features.Settings.Interfaces;
using ControlTowerWin.Features.Settings.Services;
using ControlTowerWin.Features.Settings.ViewModels;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Shell.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ISettingsStore _settingsStore;

    public TerminalSessionsViewModel Sessions { get; }

    public MainWindowViewModel()
    {
        _settingsStore = new JsonSettingsStore();
        Sessions = new TerminalSessionsViewModel();

        /* 시작 시 저장된 글꼴 적용(FR-048 재시작 유지 AC) — 세션 attach 전이라 pending으로 보관됐다가 적용 */
        var settings = _settingsStore.Load();
        Sessions.ApplyFontToAll(settings.TerminalFontFamily, settings.TerminalFontSize);
    }

    /* 설정 창용 VM 생성 — 저장 방송을 전 터미널 라이브 재적용에 배선(FN-SYS-06) */
    public SettingsViewModel CreateSettingsViewModel()
    {
        var viewModel = new SettingsViewModel(_settingsStore);
        viewModel.FontSettingsChanged += Sessions.ApplyFontToAll;
        return viewModel;
    }
}
