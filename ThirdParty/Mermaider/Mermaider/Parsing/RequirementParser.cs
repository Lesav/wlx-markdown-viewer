using System.Text.RegularExpressions;
using Mermaider.Models;

namespace Mermaider.Parsing;

internal static partial class RequirementParser
{
	private const int TimeoutMs = 2000;

	private static Regex TitlePattern() => Net48TitlePatternRegexCache.Value;
	private static class Net48TitlePatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^title\s+(.+)$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex DirectionPattern() => Net48DirectionPatternRegexCache.Value;
	private static class Net48DirectionPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^direction\s+(TB|BT|LR|RL)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex RequirementOpenPattern() => Net48RequirementOpenPatternRegexCache.Value;
	private static class Net48RequirementOpenPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^(requirement|functionalRequirement|interfaceRequirement|performanceRequirement|physicalRequirement|designConstraint)\s+(.+?)\s*\{\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex ElementOpenPattern() => Net48ElementOpenPatternRegexCache.Value;
	private static class Net48ElementOpenPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^element\s+(.+?)\s*\{\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex PropertyPattern() => Net48PropertyPatternRegexCache.Value;
	private static class Net48PropertyPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^(\w+)\s*:\s*(.+)$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	// A - type -> B
	private static Regex ForwardRelationPattern() => Net48ForwardRelationPatternRegexCache.Value;
	private static class Net48ForwardRelationPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^(.+?)\s+-\s+(contains|copies|derives|satisfies|verifies|refines|traces)\s+->\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	// B <- type - A
	private static Regex ReverseRelationPattern() => Net48ReverseRelationPatternRegexCache.Value;
	private static class Net48ReverseRelationPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^(.+?)\s+<-\s+(contains|copies|derives|satisfies|verifies|refines|traces)\s+-\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	internal static RequirementDiagram Parse(string[] lines)
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

	private static RequirementDiagram ParseCore(string[] lines)
	{
		string? title = null;
		var direction = Direction.TB;
		var requirements = new List<RequirementNode>();
		var elements = new List<RequirementElement>();
		var relations = new List<RequirementRelation>();

		for (var i = 1; i < lines.Length; i++)
		{
			var line = lines[i];

			var titleMatch = TitlePattern().Match(line);
			if (titleMatch.Success)
			{
				title = Unquote(titleMatch.Groups[1].Value.Trim());
				continue;
			}

			var dirMatch = DirectionPattern().Match(line);
			if (dirMatch.Success)
			{
				direction = ParseDirection(dirMatch.Groups[1].Value);
				continue;
			}

			var reqMatch = RequirementOpenPattern().Match(line);
			if (reqMatch.Success)
			{
				var kind = ParseKind(reqMatch.Groups[1].Value);
				var name = Unquote(reqMatch.Groups[2].Value.Trim());
				i = ParseRequirementBody(lines, i + 1, name, kind, requirements);
				continue;
			}

			var elemMatch = ElementOpenPattern().Match(line);
			if (elemMatch.Success)
			{
				var name = Unquote(elemMatch.Groups[1].Value.Trim());
				i = ParseElementBody(lines, i + 1, name, elements);
				continue;
			}

			var fwdMatch = ForwardRelationPattern().Match(line);
			if (fwdMatch.Success)
			{
				var src = Unquote(fwdMatch.Groups[1].Value.Trim());
				var relType = ParseRelationType(fwdMatch.Groups[2].Value);
				var dst = Unquote(fwdMatch.Groups[3].Value.Trim());
				relations.Add(new RequirementRelation(src, dst, relType));
				continue;
			}

			var revMatch = ReverseRelationPattern().Match(line);
			if (revMatch.Success)
			{
				// B <- type - A  means A - type -> B
				var dst = Unquote(revMatch.Groups[1].Value.Trim());
				var relType = ParseRelationType(revMatch.Groups[2].Value);
				var src = Unquote(revMatch.Groups[3].Value.Trim());
				relations.Add(new RequirementRelation(src, dst, relType));
			}
		}

		return new RequirementDiagram
		{
			Title = title,
			Direction = direction,
			Requirements = requirements,
			Elements = elements,
			Relations = relations,
		};
	}

	private static int ParseRequirementBody(
		string[] lines, int start, string name, RequirementKind kind, List<RequirementNode> requirements)
	{
		string? id = null;
		string? text = null;
		var risk = RequirementRisk.Unspecified;
		var verify = RequirementVerifyMethod.Unspecified;

		var i = start;
		for (; i < lines.Length; i++)
		{
			var line = lines[i];
			if (line is "}")
				break;

			var prop = PropertyPattern().Match(line);
			if (!prop.Success)
				continue;

			var key = prop.Groups[1].Value;
			var value = Unquote(prop.Groups[2].Value.Trim());

			if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
				id = value;
			else if (key.Equals("text", StringComparison.OrdinalIgnoreCase))
				text = value;
			else if (key.Equals("risk", StringComparison.OrdinalIgnoreCase))
				risk = ParseRisk(value);
			else if (key.Equals("verifymethod", StringComparison.OrdinalIgnoreCase)
				|| key.Equals("verifyMethod", StringComparison.OrdinalIgnoreCase))
				verify = ParseVerifyMethod(value);
		}

		requirements.Add(new RequirementNode(name, kind, id, text, risk, verify));
		return i;
	}

	private static int ParseElementBody(
		string[] lines, int start, string name, List<RequirementElement> elements)
	{
		string? type = null;
		string? docRef = null;

		var i = start;
		for (; i < lines.Length; i++)
		{
			var line = lines[i];
			if (line is "}")
				break;

			var prop = PropertyPattern().Match(line);
			if (!prop.Success)
				continue;

			var key = prop.Groups[1].Value;
			var value = Unquote(prop.Groups[2].Value.Trim());

			if (key.Equals("type", StringComparison.OrdinalIgnoreCase))
				type = value;
			else if (key.Equals("docref", StringComparison.OrdinalIgnoreCase)
				|| key.Equals("docRef", StringComparison.OrdinalIgnoreCase))
				docRef = value;
		}

		elements.Add(new RequirementElement(name, type, docRef));
		return i;
	}

	private static Direction ParseDirection(string value) => value.ToUpperInvariant() switch
	{
		"TB" or "TD" => Direction.TB,
		"BT" => Direction.BT,
		"LR" => Direction.LR,
		"RL" => Direction.RL,
		_ => Direction.TB,
	};

	private static RequirementKind ParseKind(string value) => value.ToLowerInvariant() switch
	{
		"functionalrequirement" => RequirementKind.FunctionalRequirement,
		"interfacerequirement" => RequirementKind.InterfaceRequirement,
		"performancerequirement" => RequirementKind.PerformanceRequirement,
		"physicalrequirement" => RequirementKind.PhysicalRequirement,
		"designconstraint" => RequirementKind.DesignConstraint,
		_ => RequirementKind.Requirement,
	};

	private static RequirementRisk ParseRisk(string value) => value.ToLowerInvariant() switch
	{
		"low" => RequirementRisk.Low,
		"medium" => RequirementRisk.Medium,
		"high" => RequirementRisk.High,
		_ => RequirementRisk.Unspecified,
	};

	private static RequirementVerifyMethod ParseVerifyMethod(string value) => value.ToLowerInvariant() switch
	{
		"analysis" => RequirementVerifyMethod.Analysis,
		"demonstration" => RequirementVerifyMethod.Demonstration,
		"inspection" => RequirementVerifyMethod.Inspection,
		"test" => RequirementVerifyMethod.Test,
		_ => RequirementVerifyMethod.Unspecified,
	};

	private static RequirementRelationType ParseRelationType(string value) => value.ToLowerInvariant() switch
	{
		"contains" => RequirementRelationType.Contains,
		"copies" => RequirementRelationType.Copies,
		"derives" => RequirementRelationType.Derives,
		"satisfies" => RequirementRelationType.Satisfies,
		"verifies" => RequirementRelationType.Verifies,
		"refines" => RequirementRelationType.Refines,
		"traces" => RequirementRelationType.Traces,
		_ => RequirementRelationType.Traces,
	};

	private static string Unquote(string value)
	{
		if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
			return value[1..^1];
		return value;
	}
}
