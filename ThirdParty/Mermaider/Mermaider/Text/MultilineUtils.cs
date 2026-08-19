using System.Text;
using System.Text.RegularExpressions;

namespace Mermaider.Text;

/// <summary>
/// Shared utilities for rendering multi-line text in SVG with inline formatting.
/// </summary>
internal static partial class MultilineUtils
{
	private const int TimeoutMs = 2000;

	private const string XmlSpecialCharacters = "&<>\"'";

	private static Regex BrTagPattern() => Net48BrTagPatternRegexCache.Value;
	private static class Net48BrTagPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex EscapedNewlinePattern() => Net48EscapedNewlinePatternRegexCache.Value;
	private static class Net48EscapedNewlinePatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"\\n", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex UnsupportedTagPattern() => Net48UnsupportedTagPatternRegexCache.Value;
	private static class Net48UnsupportedTagPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"</?(?:sub|sup|small|mark)\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex MarkdownBoldPattern() => Net48MarkdownBoldPatternRegexCache.Value;
	private static class Net48MarkdownBoldPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"\*\*(.+?)\*\*", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex MarkdownItalicPattern() => Net48MarkdownItalicPatternRegexCache.Value;
	private static class Net48MarkdownItalicPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"(?<!\*)\*([^\s*](?:[^*]*[^\s*])?)\*(?!\*)", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex MarkdownStrikethroughPattern() => Net48MarkdownStrikethroughPatternRegexCache.Value;
	private static class Net48MarkdownStrikethroughPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"~~(.+?)~~", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex FormatTagPattern() => Net48FormatTagPatternRegexCache.Value;
	private static class Net48FormatTagPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"<(/?)(b|strong|i|em|u|s|del)\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	private static Regex HasFormatTagPattern() => Net48HasFormatTagPatternRegexCache.Value;
	private static class Net48HasFormatTagPatternRegexCache
	{
		internal static readonly Regex Value = new Regex(@"</?(?:b|strong|i|em|u|s|del)\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(TimeoutMs));
	}

	/// <summary>
	/// Normalize label text: strip quotes, convert &lt;br&gt; tags and \n to newlines,
	/// strip unsupported tags, convert markdown formatting to HTML tags.
	/// </summary>
	internal static string NormalizeBrTags(string label)
	{
		var unquoted = label is ['"', .., '"'] ? label[1..^1] : label;
		var result = BrTagPattern().Replace(unquoted, "\n");
		result = EscapedNewlinePattern().Replace(result, "\n");
		result = UnsupportedTagPattern().Replace(result, "");
		result = MarkdownBoldPattern().Replace(result, "<b>$1</b>");
		result = MarkdownItalicPattern().Replace(result, "<i>$1</i>");
		result = MarkdownStrikethroughPattern().Replace(result, "<s>$1</s>");
		return result;
	}

	internal static void AppendEscapedXml(StringBuilder sb, ReadOnlySpan<char> value)
	{
		if (!ContainsAny(value, XmlSpecialCharacters))
		{
			_ = sb.Append(value.ToString());
			return;
		}

		foreach (var c in value)
		{
			_ = c switch
			{
				'&' => sb.Append("&amp;"),
				'<' => sb.Append("&lt;"),
				'>' => sb.Append("&gt;"),
				'"' => sb.Append("&quot;"),
				'\'' => sb.Append("&#39;"),
				_ => sb.Append(c)
			};
		}
	}

	/// <summary>
	/// Returns <paramref name="value"/> with XML attribute-significant characters escaped.
	/// Used to neutralize attribute-breakout injection when a user-supplied string
	/// (e.g. a <c>style</c>/<c>classDef</c> color) is emitted into a double-quoted SVG
	/// attribute value. Returns the original instance unchanged when nothing needs escaping.
	/// </summary>
	internal static string EscapeAttr(string value)
	{
		if (!ContainsAny(value.AsSpan(), XmlSpecialCharacters))
			return value;

		var sb = new StringBuilder(value.Length + 16);
		AppendEscapedAttr(sb, value.AsSpan());
		return sb.ToString();
	}

	internal static void AppendEscapedAttr(StringBuilder sb, ReadOnlySpan<char> value)
	{
		if (!ContainsAny(value, XmlSpecialCharacters))
		{
			_ = sb.Append(value.ToString());
			return;
		}

		foreach (var c in value)
		{
			_ = c switch
			{
				'&' => sb.Append("&amp;"),
				'"' => sb.Append("&quot;"),
				'<' => sb.Append("&lt;"),
				'>' => sb.Append("&gt;"),
				_ => sb.Append(c)
			};
		}
	}

	private static bool ContainsAny(ReadOnlySpan<char> value, string characters)
	{
		foreach (var c in value)
		{
			if (characters.IndexOf(c) >= 0)
				return true;
		}
		return false;
	}

	internal static void AppendLineContent(StringBuilder sb, ReadOnlySpan<char> line)
	{
		if (!line.Contains('<'))
		{
			AppendEscapedXml(sb, line);
			return;
		}

		var lineStr = line.ToString();
		if (!HasFormatTagPattern().IsMatch(lineStr))
		{
			AppendEscapedXml(sb, line);
			return;
		}

		var bold = false;
		var italic = false;
		var underline = false;
		var strikethrough = false;
		var lastIndex = 0;

		foreach (Match m in FormatTagPattern().Matches(lineStr))
		{
			if (m.Index > lastIndex)
				AppendStyledSegment(sb, lineStr.AsSpan(lastIndex, m.Index - lastIndex), bold, italic, underline, strikethrough);

			lastIndex = m.Index + m.Length;
			var isClosing = m.Groups[1].Value == "/";
			var tag = m.Groups[2].Value.ToLowerInvariant();

			switch (tag)
			{
				case "b" or "strong":
					bold = !isClosing;
					break;
				case "i" or "em":
					italic = !isClosing;
					break;
				case "u":
					underline = !isClosing;
					break;
				case "s" or "del":
					strikethrough = !isClosing;
					break;
			}
		}

		if (lastIndex < lineStr.Length)
			AppendStyledSegment(sb, lineStr.AsSpan(lastIndex), bold, italic, underline, strikethrough);
	}

	private static void AppendStyledSegment(
		StringBuilder sb,
		ReadOnlySpan<char> text,
		bool bold, bool italic, bool underline, bool strikethrough)
	{
		if (!bold && !italic && !underline && !strikethrough)
		{
			AppendEscapedXml(sb, text);
			return;
		}

		_ = sb.Append("<tspan");
		if (bold)
			_ = sb.Append(" font-weight=\"bold\"");
		if (italic)
			_ = sb.Append(" font-style=\"italic\"");
		if (underline || strikethrough)
		{
			_ = sb.Append(" text-decoration=\"");
			if (underline)
				_ = sb.Append("underline");
			if (underline && strikethrough)
				_ = sb.Append(' ');
			if (strikethrough)
				_ = sb.Append("line-through");
			_ = sb.Append('"');
		}
		_ = sb.Append('>');
		AppendEscapedXml(sb, text);
		_ = sb.Append("</tspan>");
	}

	internal static void AppendMultilineText(
		StringBuilder sb,
		string text,
		double cx, double cy,
		double fontSize,
		string attrs,
		double baselineShift = 0.35)
	{
		var lines = text.Split('\n');

		if (lines.Length == 1)
		{
			var dy = fontSize * baselineShift;
			_ = sb.Append("<text x=\"").Append(cx).Append("\" y=\"").Append(cy)
				.Append("\" ").Append(attrs)
				.Append(" dy=\"").Append(dy).Append("\">");
			AppendLineContent(sb, text.AsSpan());
			_ = sb.Append("</text>");
			return;
		}

		var lineHeight = fontSize * TextMetrics.LineHeightRatio;
		var firstDy = (-((lines.Length - 1) / 2.0) * lineHeight) + (fontSize * baselineShift);

		_ = sb.Append("<text x=\"").Append(cx).Append("\" y=\"").Append(cy)
			.Append("\" ").Append(attrs).Append('>');

		for (var i = 0; i < lines.Length; i++)
		{
			var dy = i == 0 ? firstDy : lineHeight;
			_ = sb.Append("<tspan x=\"").Append(cx).Append("\" dy=\"").Append(dy).Append("\">");
			AppendLineContent(sb, lines[i].AsSpan());
			_ = sb.Append("</tspan>");
		}

		_ = sb.Append("</text>");
	}

	internal static void AppendMultilineTextWithBackground(
		StringBuilder sb,
		string text,
		double cx, double cy,
		double textWidth, double textHeight,
		double fontSize,
		double padding,
		string textAttrs,
		string bgAttrs)
	{
		var bgWidth = textWidth + (padding * 2);
		var bgHeight = textHeight + (padding * 2);

		_ = sb.Append("<rect x=\"").Append(cx - (bgWidth / 2))
			.Append("\" y=\"").Append(cy - (bgHeight / 2))
			.Append("\" width=\"").Append(bgWidth)
			.Append("\" height=\"").Append(bgHeight)
			.Append("\" ").Append(bgAttrs).Append(" />\n");

		AppendMultilineText(sb, text, cx, cy, fontSize, textAttrs);
	}
}
