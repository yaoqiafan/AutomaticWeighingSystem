using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace AWS.UI.Charts;

/// <summary>
/// LiveCharts 文字画刷工厂。
/// LiveCharts 默认使用不含 CJK 字形的字体，中文会渲染成方框；
/// 这里统一注入微软雅黑字体，供各图表坐标轴标签/名称使用。
/// </summary>
public static class ChartPaints
{
    /// <summary>带中文字体的灰色文字画刷（字号由 Axis.TextSize 控制）。</summary>
    public static SolidColorPaint Text()
        => Text(0x88, 0x88, 0x88);

    public static SolidColorPaint Text(byte r, byte g, byte b)
        => new(new SKColor(r, g, b))
        {
            SKTypeface = SKTypeface.FromFamilyName("Microsoft YaHei")
        };

    /// <summary>Tooltip / Legend 文字画刷（深色，带中文字体）。
    /// 供 CartesianChart 的 TooltipTextPaint / LegendTextPaint 通过 x:Static 引用。</summary>
    public static SolidColorPaint TooltipText { get; } = new(new SKColor(0x33, 0x33, 0x33))
    {
        SKTypeface = SKTypeface.FromFamilyName("Microsoft YaHei")
    };
}
