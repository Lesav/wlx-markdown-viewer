using System.Globalization;
using System.Text.RegularExpressions;
using Mermaider.Models;

namespace Mermaider.Parsing;

internal static partial class RadarParser
{
	private const int TimeoutMs = 2000;

	private static Regex TitlePattern() => Net48TitlePatternRegexCache.Value;
	private static class Net48TitlePatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^title\s+(.+)$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex AxisPattern() => Net48AxisPatternRegexCache.Value;
	private static class Net48AxisPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^axis\s+(.+)$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex CurvePattern() => Net48CurvePatternRegexCache.Value;
	private static class Net48CurvePatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^curve\s+(.+)$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex CurveDetailPattern() => Net48CurveDetailPatternRegexCache.Value;
	private static class Net48CurveDetailPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^(?:""([^""]+)""|(\S+?))\s*(?:\[""([^""]+)""\])?\s*\{(.+?)\}", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex MaxPattern() => Net48MaxPatternRegexCache.Value;
	private static class Net48MaxPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^max\s+(\d+(?:\.\d+)?)\s*$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex MinPattern() => Net48MinPatternRegexCache.Value;
	private static class Net48MinPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^min\s+(\d+(?:\.\d+)?)\s*$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex TicksPattern() => Net48TicksPatternRegexCache.Value;
	private static class Net48TicksPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^ticks\s+(\d+)\s*$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex GraticulePattern() => Net48GraticulePatternRegexCache.Value;
	private static class Net48GraticulePatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^graticule\s+(circle|polygon)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex ShowLegendPattern() => Net48ShowLegendPatternRegexCache.Value;
	private static class Net48ShowLegendPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^showLegend\s+(true|false)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	internal static RadarChart Parse(string[] lines)
	{
		try
		{
			return ParseCore(lines);
		}
		catch (RegexMatchTimeoutException ex)
		{
			throw new MermaidParseException(
				$"Parsing timed out after {ex.MatchTimeout.TotalSeconds}s — input may contain pathological patterns.",
				ex);
		}
	}

	private static RadarChart ParseCore(string[] lines)
	{
		string? title = null;
		var axes = new List<RadarAxis>();
		var curves = new List<RadarCurve>();
		var min = 0.0;
		var max = 0.0;
		var maxSet = false;
		var ticks = 5;
		var graticule = RadarGraticule.Circle;
		var showLegend = true;

		for (var i = 1; i < lines.Length; i++)
		{
			var line = lines[i];

			var titleMatch = TitlePattern().Match(line);
			if (titleMatch.Success)
			{
				title = titleMatch.Groups[1].Value.Trim();
				continue;
			}

			var axisMatch = AxisPattern().Match(line);
			if (axisMatch.Success)
			{
				ParseAxes(axisMatch.Groups[1].Value, axes);
				continue;
			}

			var curveMatch = CurvePattern().Match(line);
			if (curveMatch.Success)
			{
				ParseCurves(curveMatch.Groups[1].Value, curves);
				continue;
			}

			var maxMatch = MaxPattern().Match(line);
			if (maxMatch.Success)
			{
				max = double.Parse(maxMatch.Groups[1].Value, CultureInfo.InvariantCulture);
				maxSet = true;
				continue;
			}

			var minMatch = MinPattern().Match(line);
			if (minMatch.Success)
			{
				min = double.Parse(minMatch.Groups[1].Value, CultureInfo.InvariantCulture);
				continue;
			}

			var ticksMatch = TicksPattern().Match(line);
			if (ticksMatch.Success)
			{
				ticks = int.Parse(ticksMatch.Groups[1].Value, CultureInfo.InvariantCulture);
				continue;
			}

			var gratMatch = GraticulePattern().Match(line);
			if (gratMatch.Success)
			{
				graticule = gratMatch.Groups[1].Value.Equals("polygon", StringComparison.OrdinalIgnoreCase)
					? RadarGraticule.Polygon
					: RadarGraticule.Circle;
				continue;
			}

			var legendMatch = ShowLegendPattern().Match(line);
			if (legendMatch.Success)
				showLegend = legendMatch.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
		}

		if (!maxSet)
			max = ComputeMax(curves);

		return new RadarChart
		{
			Title = title,
			Axes = axes,
			Curves = curves,
			Min = min,
			Max = max,
			Ticks = ticks,
			Graticule = graticule,
			ShowLegend = showLegend,
		};
	}

	private static void ParseAxes(string text, List<RadarAxis> axes)
	{
		foreach (var part in text.Split(','))
		{
			var trimmed = part.Trim();
			if (trimmed.Length == 0)
				continue;

			var bracketStart = trimmed.IndexOf('[');
			if (bracketStart >= 0)
			{
				var id = trimmed[..bracketStart].Trim();
				var label = trimmed[(bracketStart + 1)..].TrimEnd(']').Trim().Trim('"');
				axes.Add(new RadarAxis(id, label));
			}
			else
			{
				axes.Add(new RadarAxis(trimmed, trimmed));
			}
		}
	}

	private static void ParseCurves(string text, List<RadarCurve> curves)
	{
		var match = CurveDetailPattern().Match(text);
		while (match.Success)
		{
			var id = match.Groups[1].Success ? match.Groups[1].Value
				: match.Groups[2].Success ? match.Groups[2].Value
				: "";
			var label = match.Groups[3].Success ? match.Groups[3].Value : id;
			var valuesStr = match.Groups[4].Value;
			var values = ParseValues(valuesStr);
			curves.Add(new RadarCurve(id, label, values));
			match = match.NextMatch();
		}
	}

	private static List<double> ParseValues(string text)
	{
		var values = new List<double>();
		foreach (var part in text.Split(','))
		{
			var trimmed = part.Trim();
			var colonIdx = trimmed.IndexOf(':');
			if (colonIdx >= 0)
				trimmed = trimmed[(colonIdx + 1)..].Trim();
			if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
				values.Add(v);
		}
		return values;
	}

	private static double ComputeMax(List<RadarCurve> curves)
	{
		var max = 5.0;
		foreach (var curve in curves)
		{
			foreach (var v in curve.Values)
			{
				if (v > max)
					max = v;
			}
		}
		return max;
	}
}
