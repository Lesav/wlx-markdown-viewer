using System.Text.RegularExpressions;
using Mermaider.Models;

namespace Mermaider.Parsing;

internal static partial class DiagramDetector
{
	private const int TimeoutMs = 2000;

	private static Regex SequenceHeader() => Net48SequenceHeaderRegexCache.Value;
	private static class Net48SequenceHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^sequenceDiagram\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex ClassHeader() => Net48ClassHeaderRegexCache.Value;
	private static class Net48ClassHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^classDiagram\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex ErHeader() => Net48ErHeaderRegexCache.Value;
	private static class Net48ErHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^erDiagram\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex StateHeader() => Net48StateHeaderRegexCache.Value;
	private static class Net48StateHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^stateDiagram(-v2)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	// Keyword gate only — optional showData/title on the same line are owned by the parsers.
	private static Regex PieHeader() => Net48PieHeaderRegexCache.Value;
	private static class Net48PieHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^pie(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex QuadrantHeader() => Net48QuadrantHeaderRegexCache.Value;
	private static class Net48QuadrantHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^quadrantChart(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex TimelineHeader() => Net48TimelineHeaderRegexCache.Value;
	private static class Net48TimelineHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^timeline(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex GitGraphHeader() => Net48GitGraphHeaderRegexCache.Value;
	private static class Net48GitGraphHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^gitGraph\s*(LR:|TB:|BT:)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex RadarHeader() => Net48RadarHeaderRegexCache.Value;
	private static class Net48RadarHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^radar-beta\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex TreemapHeader() => Net48TreemapHeaderRegexCache.Value;
	private static class Net48TreemapHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^treemap-beta\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex VennHeader() => Net48VennHeaderRegexCache.Value;
	private static class Net48VennHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^venn-beta\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex MindmapHeader() => Net48MindmapHeaderRegexCache.Value;
	private static class Net48MindmapHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^mindmap\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	// Keyword gate — optional title on the same line is owned by GanttParser.
	private static Regex GanttHeader() => Net48GanttHeaderRegexCache.Value;
	private static class Net48GanttHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^gantt(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	// Keyword gate — optional title on the same line is owned by JourneyParser.
	private static Regex JourneyHeader() => Net48JourneyHeaderRegexCache.Value;
	private static class Net48JourneyHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^journey(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	// Keyword gate only — full header options live in C4Parser
	private static Regex C4Header() => Net48C4HeaderRegexCache.Value;
	private static class Net48C4HeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^C4(?:Context|Container|Component|Dynamic|Deployment)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex SankeyHeader() => Net48SankeyHeaderRegexCache.Value;
	private static class Net48SankeyHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^sankey(?:-beta)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex XyChartHeader() => Net48XyChartHeaderRegexCache.Value;
	private static class Net48XyChartHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^xychart(?:-beta)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	// Matches requirementDiagram and bare requirement (upstream detector).
	private static Regex RequirementHeader() => Net48RequirementHeaderRegexCache.Value;
	private static class Net48RequirementHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^requirement(Diagram)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex PacketHeader() => Net48PacketHeaderRegexCache.Value;
	private static class Net48PacketHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^packet(?:-beta)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex KanbanHeader() => Net48KanbanHeaderRegexCache.Value;
	private static class Net48KanbanHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^kanban\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex ArchitectureHeader() => Net48ArchitectureHeaderRegexCache.Value;
	private static class Net48ArchitectureHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^architecture(?:-beta)?(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex BlockHeader() => Net48BlockHeaderRegexCache.Value;
	private static class Net48BlockHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^block(?:-beta)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex TreeViewHeader() => Net48TreeViewHeaderRegexCache.Value;
	private static class Net48TreeViewHeaderRegexCache
	{
		internal static readonly Regex Value = new Regex(@"^treeView(?:-beta)?(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}


	internal static DiagramType Detect(ReadOnlySpan<char> text)
	{
		var firstLineEnd = text.IndexOf('\n');
		var firstLine = firstLineEnd >= 0 ? text[..firstLineEnd] : text;
		firstLine = firstLine.Trim();

		var firstLineStr = firstLine.ToString();
		if (SequenceHeader().IsMatch(firstLineStr))
			return DiagramType.Sequence;
		if (ClassHeader().IsMatch(firstLineStr))
			return DiagramType.Class;
		if (ErHeader().IsMatch(firstLineStr))
			return DiagramType.Er;
		if (StateHeader().IsMatch(firstLineStr))
			return DiagramType.State;
		if (PieHeader().IsMatch(firstLineStr))
			return DiagramType.Pie;
		if (QuadrantHeader().IsMatch(firstLineStr))
			return DiagramType.Quadrant;
		if (TimelineHeader().IsMatch(firstLineStr))
			return DiagramType.Timeline;
		if (GitGraphHeader().IsMatch(firstLineStr))
			return DiagramType.GitGraph;
		if (RadarHeader().IsMatch(firstLineStr))
			return DiagramType.Radar;
		if (TreemapHeader().IsMatch(firstLineStr))
			return DiagramType.Treemap;
		if (VennHeader().IsMatch(firstLineStr))
			return DiagramType.Venn;
		if (MindmapHeader().IsMatch(firstLineStr))
			return DiagramType.Mindmap;
		if (GanttHeader().IsMatch(firstLineStr))
			return DiagramType.Gantt;
		if (JourneyHeader().IsMatch(firstLineStr))
			return DiagramType.Journey;
		if (C4Header().IsMatch(firstLineStr))
			return DiagramType.C4;
		if (SankeyHeader().IsMatch(firstLineStr))
			return DiagramType.Sankey;
		if (XyChartHeader().IsMatch(firstLineStr))
			return DiagramType.XyChart;
		if (RequirementHeader().IsMatch(firstLineStr))
			return DiagramType.Requirement;
		if (PacketHeader().IsMatch(firstLineStr))
			return DiagramType.Packet;
		if (KanbanHeader().IsMatch(firstLineStr))
			return DiagramType.Kanban;
		if (ArchitectureHeader().IsMatch(firstLineStr))
			return DiagramType.Architecture;
		if (BlockHeader().IsMatch(firstLineStr))
			return DiagramType.Block;
		if (TreeViewHeader().IsMatch(firstLineStr))
			return DiagramType.TreeView;

		return DiagramType.Flowchart;
	}
}
