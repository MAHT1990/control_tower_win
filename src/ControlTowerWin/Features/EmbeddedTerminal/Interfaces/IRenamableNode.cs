namespace ControlTowerWin.Features.EmbeddedTerminal.Interfaces;

/// <summary>
/// 좌측 트리에서 인라인 이름 변경(FR-046 / FN-TRM-16)을 지원하는 노드 계약.
/// 탭·터미널 ViewModel이 공통 구현하여, 뷰 코드비하인드가 노드 종류(탭/터미널)에
/// 무관하게 rename(편집 시작·커밋·취소)을 다룰 수 있게 한다.
/// </summary>
public interface IRenamableNode
{
    /* 표시 이름(디스플레이 라벨). 빈 문자열 세팅은 구현체가 거부한다(이전 이름 유지). */
    string Title { get; set; }

    /* 인라인 편집 모드 토글(트리 노드의 TextBox 노출) */
    bool IsEditing { get; set; }
}
