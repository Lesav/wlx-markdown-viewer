using System.Windows.Media;

namespace MarkdownView.Wpf;

internal static class MarkdownTheme
{
    internal const string DarkBackground = "#070B10";
    internal const string DarkForeground = "#F0F6FC";
    internal const string DarkHeading = "#FFFFFF";
    internal const string DarkMuted = "#C9D1D9";
    internal const string DarkPanel = "#161B22";
    internal const string DarkBorder = "#6E7681";
    internal const string DarkLink = "#79C0FF";

    internal const string LightBackground = "#FFFFFF";
    internal const string LightForeground = "#24292F";
    internal const string LightHeading = "#1F2328";
    internal const string LightMuted = "#57606A";
    internal const string LightPanel = "#F6F8FA";
    internal const string LightBorder = "#D0D7DE";
    internal const string LightLink = "#0969DA";

    internal static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
