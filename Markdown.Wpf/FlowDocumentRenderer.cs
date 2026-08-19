using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Mermaider;
using SharpVectors.Converters;
using SharpVectors.Dom;
using SharpVectors.Renderers.Wpf;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;
using MdTable = Markdig.Extensions.Tables.Table;
using WpfList = System.Windows.Documents.List;
using WpfListItem = System.Windows.Documents.ListItem;

namespace MarkdownView.Wpf;

internal sealed class FlowDocumentRenderer
{
    private readonly bool _dark;
    private readonly string _baseDirectory;
    private readonly MarkdownPipeline _pipeline;
    private readonly Brush _background;
    private readonly Brush _foreground;
    private readonly Brush _heading;
    private readonly Brush _muted;
    private readonly Brush _panel;
    private readonly Brush _border;
    private readonly Brush _link;

    internal FlowDocumentRenderer(string fileName, string extensions, bool dark)
    {
        _dark = dark;
        _baseDirectory = Path.GetDirectoryName(Path.GetFullPath(fileName)) ?? Environment.CurrentDirectory;
        var builder = new MarkdownPipelineBuilder();
        MarkdownExtensions.Configure(builder, string.IsNullOrWhiteSpace(extensions) ? "advanced" : extensions);
        MarkdownExtensions.DisableHtml(builder);
        _pipeline = builder.Build();

        _background = MarkdownTheme.Brush(dark ? MarkdownTheme.DarkBackground : MarkdownTheme.LightBackground);
        _foreground = MarkdownTheme.Brush(dark ? MarkdownTheme.DarkForeground : MarkdownTheme.LightForeground);
        _heading = MarkdownTheme.Brush(dark ? MarkdownTheme.DarkHeading : MarkdownTheme.LightHeading);
        _muted = MarkdownTheme.Brush(dark ? MarkdownTheme.DarkMuted : MarkdownTheme.LightMuted);
        _panel = MarkdownTheme.Brush(dark ? MarkdownTheme.DarkPanel : MarkdownTheme.LightPanel);
        _border = MarkdownTheme.Brush(dark ? MarkdownTheme.DarkBorder : MarkdownTheme.LightBorder);
        _link = MarkdownTheme.Brush(dark ? MarkdownTheme.DarkLink : MarkdownTheme.LightLink);
    }

    internal FlowDocument Render(string markdown)
    {
        var parsed = Markdig.Markdown.Parse(markdown ?? string.Empty, _pipeline);
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            Foreground = _foreground,
            Background = _background,
            PagePadding = new Thickness(24, 18, 24, 28),
            LineHeight = double.NaN,
        };

        foreach (var block in parsed)
            AppendBlock(document.Blocks, block);
        return document;
    }

    private void AppendBlock(BlockCollection target, MdBlock block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                target.Add(CreateHeading(heading));
                return;
            case ParagraphBlock paragraph:
                target.Add(CreateParagraph(paragraph.Inline));
                return;
            case FencedCodeBlock fenced when IsMermaid(fenced.Info):
                target.Add(CreateMermaid(fenced.Lines.ToString()));
                return;
            case CodeBlock code:
                target.Add(CreateCodeBlock(code.Lines.ToString()));
                return;
            case ListBlock list:
                target.Add(CreateList(list));
                return;
            case QuoteBlock quote:
                target.Add(CreateQuote(quote));
                return;
            case MdTable table:
                target.Add(CreateTable(table));
                return;
            case ThematicBreakBlock:
                target.Add(new BlockUIContainer(new Border
                {
                    Height = 1,
                    Background = _border,
                    Margin = new Thickness(0, 8, 0, 8),
                }));
                return;
            case ContainerBlock container:
                foreach (var child in container)
                    AppendBlock(target, child);
                return;
            case LeafBlock leaf when leaf.Inline is not null:
                target.Add(CreateParagraph(leaf.Inline));
                return;
        }
    }

    private Paragraph CreateHeading(HeadingBlock heading)
    {
        var sizes = new[] { 30.0, 24.0, 20.0, 17.0, 15.0, 14.0 };
        var paragraph = CreateParagraph(heading.Inline);
        paragraph.FontSize = sizes[Math.Max(0, Math.Min(5, heading.Level - 1))];
        paragraph.FontWeight = FontWeights.SemiBold;
        paragraph.Foreground = _heading;
        paragraph.Margin = new Thickness(0, heading.Level <= 2 ? 18 : 12, 0, 7);
        paragraph.Padding = new Thickness(0, 0, 0, heading.Level <= 2 ? 6 : 0);
        if (heading.Level <= 2)
            paragraph.BorderBrush = _border;
        if (heading.Level <= 2)
            paragraph.BorderThickness = new Thickness(0, 0, 0, 1);
        return paragraph;
    }

    private Paragraph CreateParagraph(ContainerInline? inline)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 4, 0, 8) };
        if (inline is not null)
            AppendInlines(paragraph.Inlines, inline.FirstChild);
        return paragraph;
    }

    private void AppendInlines(InlineCollection target, MdInline? current)
    {
        while (current is not null)
        {
            switch (current)
            {
                case LiteralInline literal:
                    target.Add(new Run(literal.Content.ToString()));
                    break;
                case CodeInline code:
                    target.Add(new Run(code.Content)
                    {
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 13,
                        Background = _panel,
                        Foreground = _foreground,
                    });
                    break;
                case LineBreakInline:
                    target.Add(new LineBreak());
                    break;
                case EmphasisInline emphasis:
                    var span = emphasis.DelimiterChar == '~'
                        ? new Span { TextDecorations = TextDecorations.Strikethrough }
                        : emphasis.DelimiterCount == 2
                            ? new Bold()
                            : new Italic();
                    AppendInlines(span.Inlines, emphasis.FirstChild);
                    target.Add(span);
                    break;
                case LinkInline link when link.IsImage:
                    var image = CreateImage(link.Url, CollectText(link.FirstChild));
                    target.Add(image is null ? new Run(CollectText(link.FirstChild)) : new InlineUIContainer(image));
                    break;
                case LinkInline link:
                    var hyperlink = new Hyperlink { Foreground = _link, ToolTip = link.Url };
                    AppendInlines(hyperlink.Inlines, link.FirstChild);
                    if (hyperlink.Inlines.Count == 0)
                        hyperlink.Inlines.Add(new Run(link.Url ?? string.Empty));
                    hyperlink.Click += (_, _) => OpenLink(link.Url);
                    target.Add(hyperlink);
                    break;
                case AutolinkInline autoLink:
                    var automatic = new Hyperlink(new Run(autoLink.Url)) { Foreground = _link, ToolTip = autoLink.Url };
                    automatic.Click += (_, _) => OpenLink(autoLink.Url);
                    target.Add(automatic);
                    break;
                case HtmlEntityInline entity:
                    target.Add(new Run(entity.Transcoded.ToString()));
                    break;
                case TaskList task:
                    target.Add(new Run(task.Checked ? "☑ " : "☐ ") { Foreground = _muted });
                    break;
                case ContainerInline container:
                    AppendInlines(target, container.FirstChild);
                    break;
                default:
                    target.Add(new Run(current.ToString()));
                    break;
            }
            current = current.NextSibling;
        }
    }

    private System.Windows.Documents.Block CreateCodeBlock(string code)
    {
        var text = new TextBlock
        {
            Text = code.TrimEnd('\r', '\n'),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Foreground = _foreground,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(12),
        };
        return new BlockUIContainer(new Border
        {
            Child = text,
            Background = _panel,
            BorderBrush = _border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 5, 0, 10),
        });
    }

    private System.Windows.Documents.Block CreateMermaid(string source)
    {
        try
        {
            var svg = MermaidRenderer.RenderSvg(source, new Mermaider.Models.RenderOptions
            {
                Bg = _dark ? MarkdownTheme.DarkBackground : MarkdownTheme.LightBackground,
                Fg = _dark ? MarkdownTheme.DarkForeground : MarkdownTheme.LightForeground,
                Accent = _dark ? MarkdownTheme.DarkLink : MarkdownTheme.LightLink,
                Muted = _dark ? MarkdownTheme.DarkMuted : MarkdownTheme.LightMuted,
                Line = _dark ? MarkdownTheme.DarkBorder : "#8C959F",
                Surface = _dark ? MarkdownTheme.DarkPanel : MarkdownTheme.LightPanel,
                Border = _dark ? MarkdownTheme.DarkBorder : MarkdownTheme.LightBorder,
                Transparent = true,
            });
            svg = MakeSvgWpfCompatible(svg, _dark);
            var settings = new WpfDrawingSettings
            {
                IncludeRuntime = false,
                TextAsGeometry = false,
                OptimizePath = true,
                ExternalResourcesAccessMode = ExternalResourcesAccessModes.Ignore,
                CanUseBitmap = false,
            };
            var reader = new FileSvgReader(settings, false);
            using var text = new StringReader(svg);
            var drawing = reader.Read(text);
            if (drawing is null || drawing.Bounds.IsEmpty)
                throw new InvalidDataException("Mermaider returned an empty SVG drawing.");

            var image = new Image
            {
                Source = new DrawingImage(drawing),
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = Math.Max(320, Math.Min(1200, drawing.Bounds.Width)),
                Height = Math.Max(120, Math.Min(900, drawing.Bounds.Height)),
                Margin = new Thickness(8),
            };
            return new BlockUIContainer(new Border
            {
                Child = image,
                Background = _background,
                BorderBrush = _border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 6, 0, 12),
            });
        }
        catch (Exception exception)
        {
            return CreateCodeBlock("Mermaid: " + exception.Message + Environment.NewLine + Environment.NewLine + source);
        }
    }

    private WpfList CreateList(ListBlock source)
    {
        var list = new WpfList
        {
            MarkerStyle = source.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(18, 3, 0, 8),
        };
        foreach (var child in source)
        {
            if (child is not ListItemBlock item)
                continue;
            var listItem = new WpfListItem();
            foreach (var itemBlock in item)
                AppendBlock(listItem.Blocks, itemBlock);
            list.ListItems.Add(listItem);
        }
        return list;
    }

    private Section CreateQuote(QuoteBlock quote)
    {
        var section = new Section
        {
            Background = _panel,
            Foreground = _muted,
            Padding = new Thickness(12, 5, 10, 5),
            Margin = new Thickness(0, 5, 0, 10),
        };
        foreach (var child in quote)
            AppendBlock(section.Blocks, child);
        return section;
    }

    private System.Windows.Documents.Table CreateTable(MdTable source)
    {
        var table = new System.Windows.Documents.Table
        {
            CellSpacing = 0,
            BorderBrush = _border,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 5, 0, 12),
        };
        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        foreach (var sourceRow in source.OfType<Markdig.Extensions.Tables.TableRow>())
        {
            var row = new System.Windows.Documents.TableRow();
            if (sourceRow.IsHeader)
                row.Background = _panel;
            foreach (var sourceCell in sourceRow.OfType<Markdig.Extensions.Tables.TableCell>())
            {
                var cell = new System.Windows.Documents.TableCell
                {
                    BorderBrush = _border,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(8, 5, 8, 5),
                    FontWeight = sourceRow.IsHeader ? FontWeights.SemiBold : FontWeights.Normal,
                };
                foreach (var child in sourceCell)
                    AppendBlock(cell.Blocks, child);
                row.Cells.Add(cell);
            }
            group.Rows.Add(row);
        }
        return table;
    }

    private Image? CreateImage(string? url, string alt)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url, UriKind.Absolute, out var absolute) && !absolute.IsFile)
                return null;
            var path = Uri.TryCreate(url, UriKind.Absolute, out absolute) && absolute.IsFile
                ? absolute.LocalPath
                : Path.GetFullPath(Path.Combine(_baseDirectory, Uri.UnescapeDataString(url)));
            if (!File.Exists(path) || !IsInsideBaseDirectory(path))
                return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return new Image { Source = bitmap, MaxWidth = 900, Stretch = Stretch.Uniform, ToolTip = alt };
        }
        catch
        {
            return null;
        }
    }

    private bool IsInsideBaseDirectory(string path)
    {
        var root = _baseDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMermaid(string? info) =>
        string.Equals((info ?? string.Empty).Trim(), "mermaid", StringComparison.OrdinalIgnoreCase);

    private static string MakeSvgWpfCompatible(string svg, bool dark)
    {
        // SharpVectors targets SVG/CSS 2.1 and cannot parse Mermaider's CSS custom properties.
        // Resolve the small shared palette to concrete SVG values before handing it to WPF.
        svg = Regex.Replace(svg, @"<style(?:\s[^>]*)?>.*?</style>", string.Empty,
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        svg = Regex.Replace(svg, @"\sstyle=""--[^""]*""", string.Empty,
            RegexOptions.IgnoreCase);
        var values = dark
            ? new Dictionary<string, string>
            {
                ["--bg"] = MarkdownTheme.DarkBackground, ["--fg"] = MarkdownTheme.DarkForeground, ["--accent"] = MarkdownTheme.DarkLink,
                ["--_text"] = MarkdownTheme.DarkForeground, ["--_text-sec"] = MarkdownTheme.DarkMuted, ["--_text-muted"] = MarkdownTheme.DarkMuted,
                ["--_text-faint"] = "#A8B3BF", ["--_line"] = MarkdownTheme.DarkBorder, ["--_arrow"] = MarkdownTheme.DarkLink,
                ["--_node-fill"] = MarkdownTheme.DarkPanel, ["--_node-stroke"] = MarkdownTheme.DarkBorder,
                ["--_group-fill"] = MarkdownTheme.DarkPanel, ["--_group-hdr"] = "#21262D",
                ["--_group-stroke"] = MarkdownTheme.DarkBorder, ["--_inner-stroke"] = MarkdownTheme.DarkBorder,
                ["--_key-badge"] = "#21262D", ["--_accent-fill"] = "#1F2937",
                ["--_accent-stroke"] = MarkdownTheme.DarkLink, ["--_accent-text"] = MarkdownTheme.DarkLink,
            }
            : new Dictionary<string, string>
            {
                ["--bg"] = "#FFFFFF", ["--fg"] = "#24292F", ["--accent"] = "#0969DA",
                ["--_text"] = "#24292F", ["--_text-sec"] = "#57606A", ["--_text-muted"] = "#57606A",
                ["--_text-faint"] = "#8C959F", ["--_line"] = "#8C959F", ["--_arrow"] = "#0969DA",
                ["--_node-fill"] = "#F6F8FA", ["--_node-stroke"] = "#D0D7DE",
                ["--_group-fill"] = "#F6F8FA", ["--_group-hdr"] = "#EAEEF2",
                ["--_group-stroke"] = "#D0D7DE", ["--_inner-stroke"] = "#D0D7DE",
                ["--_key-badge"] = "#EAEEF2", ["--_accent-fill"] = "#DDF4FF",
                ["--_accent-stroke"] = "#0969DA", ["--_accent-text"] = "#0550AE",
            };
        values["--fs-xs"] = "12px";
        values["--fs-s"] = "14px";
        values["--fs-m"] = "16px";
        values["--fs-l"] = "18px";
        foreach (var value in values.OrderByDescending(item => item.Key.Length))
            svg = svg.Replace("var(" + value.Key + ")", value.Value);
        return svg;
    }

    private static string CollectText(MdInline? current)
    {
        var result = new System.Text.StringBuilder();
        while (current is not null)
        {
            if (current is LiteralInline literal)
                result.Append(literal.Content.ToString());
            else if (current is CodeInline code)
                result.Append(code.Content);
            else if (current is ContainerInline container)
                result.Append(CollectText(container.FirstChild));
            current = current.NextSibling;
        }
        return result.ToString();
    }

    private static void OpenLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }

}
