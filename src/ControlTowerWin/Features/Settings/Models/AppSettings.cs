namespace ControlTowerWin.Features.Settings.Models;

/// <summary>
/// 앱 전역 설정(ENT-015 부분 구현 — v1은 터미널 글꼴 필드만, FR-048).
/// %APPDATA%\ControlTowerWin\settings.json에 원자적으로 영속된다(NFR-011).
/// </summary>
public class AppSettings
{
    public const int MinFontSize = 6;
    public const int MaxFontSize = 72;
    public const string DefaultFontFamily = "Cascadia Code";
    public const int DefaultFontSize = 12;

    public string TerminalFontFamily { get; set; } = DefaultFontFamily;

    public int TerminalFontSize { get; set; } = DefaultFontSize;

    public int SchemaVersion { get; set; } = 1;

    /* 범위 밖 크기·빈 글꼴을 기본값으로 정규화(CON-13: CHECK 6..72) */
    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(TerminalFontFamily))
        {
            TerminalFontFamily = DefaultFontFamily;
        }
        if (TerminalFontSize < MinFontSize || TerminalFontSize > MaxFontSize)
        {
            TerminalFontSize = DefaultFontSize;
        }
    }
}
