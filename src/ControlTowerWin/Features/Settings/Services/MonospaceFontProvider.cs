using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace ControlTowerWin.Features.Settings.Services;

/// <summary>
/// 시스템 설치 폰트 중 고정폭(monospace)만 열거한다(FR-048 — 목록 한정으로 정렬 깨짐 방지).
/// WPF managed API에 isFixedPitch가 없어 대표 글리프 advance width 비교로 판정하며,
/// 수백 패밀리×글리프 로드가 느릴 수 있어 결과를 1회 캐시한다.
/// </summary>
public static class MonospaceFontProvider
{
    private static readonly char[] ProbeChars = { 'i', 'l', 'W', 'M', 'x', ' ' };

    private static IReadOnlyList<string>? _cache;

    public static IReadOnlyList<string> GetMonospaceFamilies()
    {
        return _cache ??= Fonts.SystemFontFamilies
            .Where(IsMonospace)
            .Select(f => f.Source)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /* 대표 글자들의 advance width가 전부 같으면 고정폭으로 판정 */
    private static bool IsMonospace(FontFamily family)
    {
        try
        {
            foreach (var typeface in family.GetTypefaces())
            {
                if (!typeface.TryGetGlyphTypeface(out var glyph)) continue;

                double? width = null;
                foreach (var ch in ProbeChars)
                {
                    if (!glyph.CharacterToGlyphMap.TryGetValue(ch, out var index)) return false;
                    double advance = glyph.AdvanceWidths[index];
                    if (width is null) width = advance;
                    else if (Math.Abs(advance - width.Value) > 1e-6) return false;
                }
                return width != null;
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
