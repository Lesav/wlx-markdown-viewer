using System.Text.RegularExpressions;

namespace Mermaider.Text;

/// <summary>Metrics for a multi-line text block.</summary>
internal readonly record struct MultilineMetrics(double Width, double Height, int LineCount, double LineHeight);

/// <summary>
/// Font-agnostic text width estimation using character-class buckets.
/// All measurement operates on <see cref="ReadOnlySpan{T}"/> to avoid allocations.
/// </summary>
internal static partial class TextMetrics
{
	internal const double LineHeightRatio = 1.3;

	private const int TimeoutMs = 2000;

	private static Regex FormattingTagPattern() => Net48FormattingTagPatternRegexCache.Value;
	private static class Net48FormattingTagPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"</?(?:b|strong|i|em|u|s|del)\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	internal static double MeasureTextWidth(ReadOnlySpan<char> text, double fontSize, int fontWeight)
	{
		var baseRatio = fontWeight >= 600 ? 0.60 : fontWeight >= 500 ? 0.57 : 0.54;

		var totalWidth = 0.0;
		foreach (var c in text)
			totalWidth += CharWidths.GetCharWidth(c);

		var minPadding = fontSize * 0.15;
		return (totalWidth * fontSize * baseRatio) + minPadding;
	}

	internal static double MeasureTextWidth(string text, double fontSize, int fontWeight) =>
		MeasureTextWidth(text.AsSpan(), fontSize, fontWeight);

	internal static double MeasurePlainWidth(ReadOnlySpan<char> line, double fontSize, int fontWeight)
	{
		if (!line.Contains('<'))
			return MeasureTextWidth(line, fontSize, fontWeight);

		var plain = FormattingTagPattern().Replace(line.ToString(), "");
		return MeasureTextWidth(plain, fontSize, fontWeight);
	}

	internal static double MeasurePlainWidth(string line, double fontSize, int fontWeight) =>
		MeasurePlainWidth(line.AsSpan(), fontSize, fontWeight);

	internal static MultilineMetrics MeasureMultiline(ReadOnlySpan<char> text, double fontSize, int fontWeight)
	{
		var lineHeight = fontSize * LineHeightRatio;
		var lineCount = 0;
		var maxWidth = 0.0;

		var start = 0;
		while (start <= text.Length)
		{
			var remaining = text.Slice(start);
			var newline = remaining.IndexOf('\n');
			var line = newline >= 0 ? remaining.Slice(0, newline) : remaining;
			lineCount++;
			var w = MeasurePlainWidth(line, fontSize, fontWeight);
			if (w > maxWidth)
				maxWidth = w;
			if (newline < 0)
				break;
			start += newline + 1;
		}

		if (lineCount == 0)
			lineCount = 1;

		return new MultilineMetrics(maxWidth, lineCount * lineHeight, lineCount, lineHeight);
	}

	internal static MultilineMetrics MeasureMultiline(string text, double fontSize, int fontWeight) =>
		MeasureMultiline(text.AsSpan(), fontSize, fontWeight);

	internal static double EstimateMonoTextWidth(ReadOnlySpan<char> text, double fontSize) =>
		text.Length * fontSize * 0.6;

	internal static double EstimateMonoTextWidth(string text, double fontSize) =>
		EstimateMonoTextWidth(text.AsSpan(), fontSize);
}
