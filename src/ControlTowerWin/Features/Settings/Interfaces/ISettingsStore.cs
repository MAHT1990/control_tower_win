using ControlTowerWin.Features.Settings.Models;

namespace ControlTowerWin.Features.Settings.Interfaces;

/// <summary>
/// 앱 설정 영속 계약(FR-042 인프라·FR-048 소비). 구현은 원자적 저장(NFR-011)을 보장한다.
/// </summary>
public interface ISettingsStore
{
    /* 설정 로드. 파일 부재·손상 시 기본값 반환(graceful). */
    AppSettings Load();

    /* 설정 저장. temp→rename 원자 커밋(NFR-011). */
    void Save(AppSettings settings);
}
