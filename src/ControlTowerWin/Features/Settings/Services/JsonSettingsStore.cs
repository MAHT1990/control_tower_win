using System;
using System.IO;
using System.Text.Json;
using ControlTowerWin.Features.Settings.Interfaces;
using ControlTowerWin.Features.Settings.Models;

namespace ControlTowerWin.Features.Settings.Services;

/// <summary>
/// JSON 파일 기반 설정 저장소(TS-06). %APPDATA%\ControlTowerWin\settings.json에
/// temp→rename 원자 커밋으로 저장한다(NFR-011 — 동일 볼륨 NTFS 원자 교체, 실패 시 원본 보존).
/// </summary>
public class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public JsonSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ControlTowerWin", "settings.json"))
    {
    }

    public JsonSettingsStore(string path) => _path = path;

    /* 파일 부재·손상 라인은 기본값으로 graceful 처리(방어적 로드) */
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Normalize();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, _path, overwrite: true);
    }
}
