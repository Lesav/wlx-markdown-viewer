using System.Text.RegularExpressions;
using Mermaider.Models;

namespace Mermaider.Parsing;

/// <summary>
/// Extracts <c>accTitle</c> and <c>accDescr</c> accessibility directives from diagram lines.
/// Consumes matched lines so downstream parsers never see them.
/// </summary>
internal static partial class AccessibilityParser
{
	private const int TimeoutMs = 2000;

	private static Regex AccTitlePattern() => Net48AccTitlePatternRegexCache.Value;
	private static class Net48AccTitlePatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^accTitle\s*:\s*(.+)$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex AccDescrSinglePattern() => Net48AccDescrSinglePatternRegexCache.Value;
	private static class Net48AccDescrSinglePatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^accDescr\s*:\s*(.+)$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex AccDescrMultiOpenPattern() => Net48AccDescrMultiOpenPatternRegexCache.Value;
	private static class Net48AccDescrMultiOpenPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^accDescr\s*\{", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	/// <summary>
	/// Extract accessibility info from <paramref name="lines"/> and return
	/// a filtered array with the accessibility lines removed.
	/// </summary>
	internal static (AccessibilityInfo Accessibility, string[] FilteredLines) Extract(string[] lines)
	{
		string? title = null;
		string? description = null;
		var filtered = new List<string>(lines.Length);

		for (var i = 0; i < lines.Length; i++)
		{
			var line = lines[i];

			var titleMatch = AccTitlePattern().Match(line);
			if (titleMatch.Success)
			{
				title = titleMatch.Groups[1].Value.Trim();
				continue;
			}

			var descrSingleMatch = AccDescrSinglePattern().Match(line);
			if (descrSingleMatch.Success)
			{
				description = descrSingleMatch.Groups[1].Value.Trim();
				continue;
			}

			if (AccDescrMultiOpenPattern().IsMatch(line))
			{
				var descrLines = new List<string>();
				i++;
				while (i < lines.Length)
				{
					var inner = lines[i];
					if (inner.TrimEnd() == "}" || inner.TrimStart().StartsWith('}'))
						break;
					descrLines.Add(inner.Trim());
					i++;
				}
				description = string.Join("\n", descrLines);
				continue;
			}

			filtered.Add(line);
		}

		var info = new AccessibilityInfo { Title = title, Description = description };
		return (info, filtered.ToArray());
	}
}
