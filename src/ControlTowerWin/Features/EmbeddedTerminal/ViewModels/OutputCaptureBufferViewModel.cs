using ControlTowerWin.Shared.Core;

namespace ControlTowerWin.Features.EmbeddedTerminal.ViewModels;

/// <summary>
/// 세션 간 출력 라우팅용 캡처 버퍼 (FR-047 / ENT-019, 런타임·비영속).
/// A 세션 출력을 수집(Capture)하여 사용자가 편집(가공)한 뒤, 대상 세션 입력으로 주입한다.
/// mechanism (c): 캡처 버퍼 + 가공 후 주입.
/// </summary>
public class OutputCaptureBufferViewModel : ViewModelBase
{
    private string _sourceAs = string.Empty;
    private string _content = string.Empty;

    /* 출처 세션 표시명 */
    public string SourceAs
    {
        get => _sourceAs;
        set { _sourceAs = value; OnPropertyChanged(); }
    }

    /* 캡처 원문(사용자 편집 가능) */
    public string Content
    {
        get => _content;
        set
        {
            _content = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasContent));
        }
    }

    public bool HasContent => !string.IsNullOrEmpty(_content);

    /* A 세션 출력을 버퍼에 적재 */
    public void Capture(string sourceLabel, string content)
    {
        SourceAs = sourceLabel;
        Content = content;
    }
}
