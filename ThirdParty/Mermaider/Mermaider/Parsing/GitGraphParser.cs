using System.Globalization;
using System.Text.RegularExpressions;
using Mermaider.Models;

namespace Mermaider.Parsing;

internal static partial class GitGraphParser
{
	private const int TimeoutMs = 2000;

	private static Regex CommitPattern() => Net48CommitPatternRegexCache.Value;
	private static class Net48CommitPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^commit(?:\s+(.+))?$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex BranchPattern() => Net48BranchPatternRegexCache.Value;
	private static class Net48BranchPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^branch\s+""?([^""\s]+)""?(?:\s+order:\s*(\d+))?$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex CheckoutPattern() => Net48CheckoutPatternRegexCache.Value;
	private static class Net48CheckoutPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^(?:checkout|switch)\s+""?([^""]+)""?$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex MergePattern() => Net48MergePatternRegexCache.Value;
	private static class Net48MergePatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^merge\s+""?([^""]+?)""?(?:\s+(.+))?$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex CherryPickPattern() => Net48CherryPickPatternRegexCache.Value;
	private static class Net48CherryPickPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^cherry-pick\s+(.+)$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex IdAttr() => Net48IdAttrRegexCache.Value;
	private static class Net48IdAttrRegexCache
	{
		internal static readonly Regex Value = new Regex(@"id:\s*""([^""]+)""", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex TagAttr() => Net48TagAttrRegexCache.Value;
	private static class Net48TagAttrRegexCache
	{
		internal static readonly Regex Value = new Regex(@"tag:\s*""([^""]+)""", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex TypeAttr() => Net48TypeAttrRegexCache.Value;
	private static class Net48TypeAttrRegexCache
	{
		internal static readonly Regex Value = new Regex(@"type:\s*(NORMAL|HIGHLIGHT|REVERSE)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex ParentAttr() => Net48ParentAttrRegexCache.Value;
	private static class Net48ParentAttrRegexCache
	{
		internal static readonly Regex Value = new Regex(@"parent:\s*""([^""]+)""", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	internal static GitGraph Parse(string[] lines)
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

	private static GitGraph ParseCore(string[] lines)
	{
		var actions = new List<GitAction>();
		var orientation = GitGraphOrientation.LR;

		var firstLine = lines[0];
		if (firstLine.Contains("TB:", StringComparison.OrdinalIgnoreCase))
			orientation = GitGraphOrientation.TB;
		else if (firstLine.Contains("BT:", StringComparison.OrdinalIgnoreCase))
			orientation = GitGraphOrientation.BT;

		for (var i = 1; i < lines.Length; i++)
		{
			var line = lines[i];

			var branchMatch = BranchPattern().Match(line);
			if (branchMatch.Success)
			{
				int? order = branchMatch.Groups[2].Success ? int.Parse(branchMatch.Groups[2].Value, CultureInfo.InvariantCulture) : null;
				actions.Add(new GitBranchAction(branchMatch.Groups[1].Value, order));
				continue;
			}

			var checkoutMatch = CheckoutPattern().Match(line);
			if (checkoutMatch.Success)
			{
				actions.Add(new GitCheckoutAction(checkoutMatch.Groups[1].Value));
				continue;
			}

			var mergeMatch = MergePattern().Match(line);
			if (mergeMatch.Success)
			{
				var rest = mergeMatch.Groups[2].Success ? mergeMatch.Groups[2].Value : "";
				actions.Add(new GitMergeAction(
					mergeMatch.Groups[1].Value,
					ExtractAttr(IdAttr(), rest),
					ExtractAttr(TagAttr(), rest),
					ExtractType(rest)));
				continue;
			}

			var cherryMatch = CherryPickPattern().Match(line);
			if (cherryMatch.Success)
			{
				var rest = cherryMatch.Groups[1].Value;
				actions.Add(new GitCherryPickAction(
					ExtractAttr(IdAttr(), rest) ?? "",
					ExtractAttr(ParentAttr(), rest)));
				continue;
			}

			var commitMatch = CommitPattern().Match(line);
			if (commitMatch.Success)
			{
				var rest = commitMatch.Groups[1].Success ? commitMatch.Groups[1].Value : "";
				actions.Add(new GitCommitAction
				{
					Id = ExtractAttr(IdAttr(), rest),
					Tag = ExtractAttr(TagAttr(), rest),
					Type = ExtractType(rest),
				});
			}
		}

		return new GitGraph { Actions = actions, Orientation = orientation };
	}

	private static string? ExtractAttr(Regex pattern, string text)
	{
		var match = pattern.Match(text);
		return match.Success ? match.Groups[1].Value : null;
	}

	private static GitCommitType ExtractType(string text)
	{
		var match = TypeAttr().Match(text);
		if (!match.Success)
			return GitCommitType.Normal;
		return match.Groups[1].Value.ToUpperInvariant() switch
		{
			"HIGHLIGHT" => GitCommitType.Highlight,
			"REVERSE" => GitCommitType.Reverse,
			_ => GitCommitType.Normal,
		};
	}
}
