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
    public static SolidColorPaint Text() => Text(0x88, 0x88, 0x88);

    public static SolidColorPaint Text(byte r, byte g, byte b)
        => new(new SKColor(r, g, b))
        {
            SKTypeface = SKTypeface.FromFamilyName("Microsoft YaHei")
        };

    // 向后兼容：DashboardView / WeighingView 通过 x:Static 引用（浅色主题固定值）
    public static SolidColorPaint TooltipText { get; } = Text(0x33, 0x33, 0x33);

    // 主题感知：坐标轴标签
    public static SolidColorPaint ThemedText(bool isDark)
        => isDark ? Text(0xAA, 0xAA, 0xAA) : Text(0x88, 0x88, 0x88);

    // 主题感知：Tooltip / Legend 文字
    public static SolidColorPaint ThemedTooltip(bool isDark)
        => isDark ? Text(0xEE, 0xEE, 0xEE) : Text(0x33, 0x33, 0x33);

    // 主题感知：条形/饼图 DataLabels
    public static SolidColorPaint ThemedDataLabel(bool isDark)
        => isDark ? Text(0xDD, 0xDD, 0xDD) : Text(0x22, 0x22, 0x22);
}
