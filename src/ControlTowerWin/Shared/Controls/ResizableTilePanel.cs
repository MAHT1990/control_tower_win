using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ControlTowerWin.Shared.Controls;

/// <summary>
/// 자식들을 자동 타일 그리드(UniformGrid식: cols=⌈√n⌉, rows=⌈n/cols⌉)로 배치하되,
/// 셀 사이 gutter(분할 핸들)를 드래그해 인접 열·행 크기를 재분배하는 분할 패널
/// (FR-044 / FN-TRM-14 / SC-10). 열·행 비율은 런타임 상태(ENT-003.pane_layout, 비영속)로
/// 패널 인스턴스에 보존된다 — 탭 keep-alive 덕에 탭 전환에도 유지된다.
/// keep-alive: 자식(터미널 View, HwndHost)을 제거·재부모화하지 않아 ConPTY 세션이 유지된다.
/// gutter는 native pane 사이 WPF 공백이라 airspace 안전, 드래그는 Mouse.Capture로 수신한다.
/// </summary>
public class ResizableTilePanel : Panel
{
    private const double Gutter = 6.0;   /* 분할 핸들(gutter) 두께 */
    private const double MinCell = 40.0; /* 최소 크기 가드(AC②) */

    private double[] _colFrac = Array.Empty<double>();
    private double[] _rowFrac = Array.Empty<double>();
    private int _cols;
    private int _rows;

    /* arrange 결과(마우스 히트 테스트용) */
    private double[] _colOffsets = Array.Empty<double>();
    private double[] _colWidths = Array.Empty<double>();
    private double[] _rowOffsets = Array.Empty<double>();
    private double[] _rowHeights = Array.Empty<double>();

    /* 드래그 상태 */
    private bool _dragging;
    private bool _dragVertical;
    private int _dragIndex;   /* 경계 인덱스(우측/아래 셀) */
    private Point _dragStart;
    private double _origA;     /* 드래그 시작 시 인접 두 셀 비율 */
    private double _origB;

    public ResizableTilePanel()
    {
        /* gutter(자식 없는 공백) 위 마우스를 수신하려면 투명 배경 필요 */
        Background = Brushes.Transparent;
    }

    /* 자식 수 n에서 타일 격자(cols·rows) 산출 + 비율 배열 크기 정합(변하면 균등 리셋) */
    private void EnsureGrid(int n)
    {
        int cols = n <= 1 ? 1 : (int)Math.Ceiling(Math.Sqrt(n));
        int rows = n == 0 ? 1 : (int)Math.Ceiling((double)n / cols);
        if (cols != _cols || _colFrac.Length != cols)
        {
            _cols = cols;
            _colFrac = Uniform(cols);
        }
        if (rows != _rows || _rowFrac.Length != rows)
        {
            _rows = rows;
            _rowFrac = Uniform(rows);
        }
    }

    private static double[] Uniform(int k)
    {
        var a = new double[k];
        for (int i = 0; i < k; i++) a[i] = 1.0 / k;
        return a;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int n = InternalChildren.Count;
        EnsureGrid(n);

        double w = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height;
        double cellW = Math.Max(0, w - Gutter * (_cols - 1));
        double cellH = Math.Max(0, h - Gutter * (_rows - 1));

        for (int i = 0; i < n; i++)
        {
            int col = i % _cols;
            int row = i / _cols;
            InternalChildren[i].Measure(new Size(cellW * _colFrac[col], cellH * _rowFrac[row]));
        }
        return new Size(w, h);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int n = InternalChildren.Count;
        EnsureGrid(n);

        double cellW = Math.Max(0, finalSize.Width - Gutter * (_cols - 1));
        double cellH = Math.Max(0, finalSize.Height - Gutter * (_rows - 1));

        _colWidths = new double[_cols];
        _colOffsets = new double[_cols];
        double x = 0;
        for (int c = 0; c < _cols; c++)
        {
            _colWidths[c] = cellW * _colFrac[c];
            _colOffsets[c] = x;
            x += _colWidths[c] + Gutter;
        }

        _rowHeights = new double[_rows];
        _rowOffsets = new double[_rows];
        double y = 0;
        for (int r = 0; r < _rows; r++)
        {
            _rowHeights[r] = cellH * _rowFrac[r];
            _rowOffsets[r] = y;
            y += _rowHeights[r] + Gutter;
        }

        for (int i = 0; i < n; i++)
        {
            int col = i % _cols;
            int row = i / _cols;
            InternalChildren[i].Arrange(new Rect(_colOffsets[col], _rowOffsets[row], _colWidths[col], _rowHeights[row]));
        }
        return finalSize;
    }

    /* 반환: 0=경계 없음, 1=수직 경계(열 사이), 2=수평 경계(행 사이). idx=경계 우/아래 셀 인덱스. */
    private int HitBoundary(Point p, out int idx)
    {
        idx = -1;
        for (int c = 1; c < _cols && c < _colOffsets.Length; c++)
        {
            if (p.X >= _colOffsets[c] - Gutter && p.X <= _colOffsets[c])
            {
                idx = c;
                return 1;
            }
        }
        for (int r = 1; r < _rows && r < _rowOffsets.Length; r++)
        {
            if (p.Y >= _rowOffsets[r] - Gutter && p.Y <= _rowOffsets[r])
            {
                idx = r;
                return 2;
            }
        }
        return 0;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this);
        if (_dragging)
        {
            if (_dragVertical) DragColumn(p);
            else DragRow(p);
            return;
        }
        Cursor = HitBoundary(p, out _) switch
        {
            1 => Cursors.SizeWE,
            2 => Cursors.SizeNS,
            _ => Cursors.Arrow,
        };
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var p = e.GetPosition(this);
        int hit = HitBoundary(p, out int idx);
        if (hit == 0) return;

        _dragging = true;
        _dragVertical = hit == 1;
        _dragIndex = idx;
        _dragStart = p;
        if (_dragVertical)
        {
            _origA = _colFrac[idx - 1];
            _origB = _colFrac[idx];
        }
        else
        {
            _origA = _rowFrac[idx - 1];
            _origB = _rowFrac[idx];
        }
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    /* 수직 경계 드래그 → 인접 두 열 비율 재분배(최소 크기 가드) */
    private void DragColumn(Point p)
    {
        double cellW = ActualWidth - Gutter * (_cols - 1);
        if (cellW <= 0) return;
        double minFrac = MinCell / cellW;
        double sum = _origA + _origB;
        if (sum < 2 * minFrac) return;

        double df = (p.X - _dragStart.X) / cellW;
        double a = _origA + df;
        double b = sum - a;
        if (a < minFrac) { a = minFrac; b = sum - a; }
        else if (b < minFrac) { b = minFrac; a = sum - b; }

        _colFrac[_dragIndex - 1] = a;
        _colFrac[_dragIndex] = b;
        InvalidateArrange();
    }

    /* 수평 경계 드래그 → 인접 두 행 비율 재분배(최소 크기 가드) */
    private void DragRow(Point p)
    {
        double cellH = ActualHeight - Gutter * (_rows - 1);
        if (cellH <= 0) return;
        double minFrac = MinCell / cellH;
        double sum = _origA + _origB;
        if (sum < 2 * minFrac) return;

        double df = (p.Y - _dragStart.Y) / cellH;
        double a = _origA + df;
        double b = sum - a;
        if (a < minFrac) { a = minFrac; b = sum - a; }
        else if (b < minFrac) { b = minFrac; a = sum - b; }

        _rowFrac[_dragIndex - 1] = a;
        _rowFrac[_dragIndex] = b;
        InvalidateArrange();
    }
}
