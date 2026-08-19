using System.Text.RegularExpressions;
using Mermaider.Models;

namespace Mermaider.Parsing;

internal static partial class PieParser
{
	private const int TimeoutMs = 2000;

	private static Regex TitlePattern() => Net48TitlePatternRegexCache.Value;
	private static class Net48TitlePatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^title\s+(.+)$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex SlicePattern() => Net48SlicePatternRegexCache.Value;
	private static class Net48SlicePatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^""([^""]+)""\s*:\s*(\d+(?:\.\d+)?)$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	internal static PieChart Parse(string[] lines)
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

	private static PieChart ParseCore(string[] lines)
	{
		var (showData, title) = DiagramHeaderOptions.Parse(lines[0], "pie");
		var slices = new List<PieSlice>();

		for (var i = 1; i < lines.Length; i++)
		{
			var line = lines[i];

			var titleMatch = TitlePattern().Match(line);
			if (titleMatch.Success)
			{
				title = titleMatch.Groups[1].Value.Trim();
				continue;
			}

			var sliceMatch = SlicePattern().Match(line);
			if (sliceMatch.Success)
			{
				var label = sliceMatch.Groups[1].Value;
				var value = double.Parse(sliceMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
				if (value > 0)
					slices.Add(new PieSlice(label, value));
			}
		}

		return new PieChart { Title = title, ShowData = showData, Slices = slices };
	}
}
