using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ControlTowerWin.Features.Settings.Interfaces;
using ControlTowerWin.Features.Settings.Models;
using ControlTowerWin.Features.Settings.Services;
using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.Settings.ViewModels;

/// <summary>
/// 설정 화면(SC-02)의 ViewModel — v1은 터미널 글꼴 섹션(FR-048/FN-SYS-06).
/// 크기(6~72 검증)·종류(monospace 목록)를 편집·원자 저장하고,
/// 저장 시 FontSettingsChanged로 전 터미널 라이브 재적용을 방송한다.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsStore _store;
    private string _fontFamily;
    private string _fontSizeText;

    /* 저장 성공 시 (family, size) 방송 — Shell이 구독해 전 터미널 재적용 */
    public event Action<string, int>? FontSettingsChanged;

    public IReadOnlyList<string> MonospaceFamilies { get; }

    public ICommand SaveCommand { get; }

    public SettingsViewModel(ISettingsStore store)
    {
        _store = store;
        var settings = _store.Load();
        _fontFamily = settings.TerminalFontFamily;
        _fontSizeText = settings.TerminalFontSize.ToString();

        var families = MonospaceFontProvider.GetMonospaceFamilies().ToList();
        if (!families.Contains(_fontFamily, StringComparer.OrdinalIgnoreCase))
        {
            families.Insert(0, _fontFamily);
        }
        MonospaceFamilies = families;

        SaveCommand = new RelayCommand(_ => Save(), _ => IsValid);
    }

    public string FontFamily
    {
        get => _fontFamily;
        set
        {
            _fontFamily = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValid));
        }
    }

    /* 자유 수치 입력(6~72). 텍스트로 받아 검증 메시지를 인라인 제공 */
    public string FontSizeText
    {
        get => _fontSizeText;
        set
        {
            _fontSizeText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(ValidationMessage));
        }
    }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(FontFamily)
        && int.TryParse(FontSizeText, out var size)
        && size >= AppSettings.MinFontSize
        && size <= AppSettings.MaxFontSize;

    public string ValidationMessage =>
        IsValid ? string.Empty : $"글꼴 크기는 {AppSettings.MinFontSize}~{AppSettings.MaxFontSize} 사이 수치여야 합니다";

    /* 원자 저장(NFR-011) 후 라이브 재적용 방송(FR-048) */
    private void Save()
    {
        if (!IsValid) return;
        var size = int.Parse(FontSizeText);
        _store.Save(new AppSettings
        {
            TerminalFontFamily = FontFamily,
            TerminalFontSize = size,
        });
        FontSettingsChanged?.Invoke(FontFamily, size);
    }

    /* 시작 시 저장된 글꼴을 적용 대상에 전파(재시작 후 유지 AC) */
    public (string Family, int Size) CurrentFont()
    {
        var settings = _store.Load();
        return (settings.TerminalFontFamily, settings.TerminalFontSize);
    }
}
