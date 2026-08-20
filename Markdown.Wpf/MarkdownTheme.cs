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
    internal const string DarkSyntaxKeyword = "#FF7B72";
    internal const string DarkSyntaxString = "#A5D6FF";
    internal const string DarkSyntaxComment = "#A8B3BF";
    internal const string DarkSyntaxNumber = "#79C0FF";
    internal const string DarkSyntaxLiteral = "#7EE787";
    internal const string DarkSyntaxVariable = "#FFA657";
    internal const string DarkSyntaxCommand = "#D2A8FF";
    internal const string DarkCodeCopyBackground = "#B83D444D";
    internal const string DarkCodeCopyHover = "#E64F5863";
    internal const string DarkCodeCopyBorder = "#8B949E";

    internal const string LightBackground = "#FFFFFF";
    internal const string LightForeground = "#24292F";
    internal const string LightHeading = "#1F2328";
    internal const string LightMuted = "#57606A";
    internal const string LightPanel = "#F6F8FA";
    internal const string LightCodePanel = "#EEF1F4";
    internal const string LightBorder = "#D0D7DE";
    internal const string LightLink = "#0969DA";
    internal const string LightSyntaxKeyword = "#CF222E";
    internal const string LightSyntaxString = "#0A3069";
    internal const string LightSyntaxComment = "#57606A";
    internal const string LightSyntaxNumber = "#0550AE";
    internal const string LightSyntaxLiteral = "#116329";
    internal const string LightSyntaxVariable = "#953800";
    internal const string LightSyntaxCommand = "#8250DF";
    internal const string LightCodeCopyBackground = "#D857606A";
    internal const string LightCodeCopyHover = "#F2424A53";
    internal const string LightCodeCopyBorder = "#6E7781";

    internal const string CodeCopyForeground = "#FFFFFF";
    internal const string CodeCopySuccess = "#2EA043";
    internal const string CodeCopyFailure = "#CF222E";

    internal static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
