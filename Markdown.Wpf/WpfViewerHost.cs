using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Text;

namespace MarkdownView.Wpf;

public static class WpfViewerHost
{
    private const int WsChild = 0x40000000;
    private const int WsVisible = 0x10000000;
    private const int WsClipChildren = 0x02000000;
    private const int WsClipSiblings = 0x04000000;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int VkEscape = 0x1B;

    private static readonly Dictionary<IntPtr, ViewState> Views = new();

    public static IntPtr Create(IntPtr parentWindow, string fileName, string extensions, bool darkMode)
    {
        if (parentWindow == IntPtr.Zero)
            throw new ArgumentException("Parent HWND is required.", nameof(parentWindow));
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("The WPF viewer must be created on an STA thread.");

        var parameters = new HwndSourceParameters("MarkdownViewWpfChild")
        {
            ParentWindow = parentWindow,
            WindowStyle = WsChild | WsVisible | WsClipChildren | WsClipSiblings,
            Width = 1,
            Height = 1,
            UsesPerPixelOpacity = false,
        };
        var source = new HwndSource(parameters);
        var viewer = CreateViewer(darkMode);
        var state = new ViewState(source, viewer, parentWindow, fileName, extensions, darkMode);
        source.RootVisual = viewer;
        viewer.PreviewKeyDown += (_, args) => HandlePreviewKey(state, args);
        Views[source.Handle] = state;
        Load(source.Handle, fileName, extensions, darkMode);
        return source.Handle;
    }

    public static bool Load(IntPtr handle, string fileName, string extensions, bool darkMode)
    {
        if (!Views.TryGetValue(handle, out var state))
            return false;
        try
        {
            var source = File.ReadAllText(Path.GetFullPath(fileName));
            ApplyTheme(state.Viewer, darkMode);
            state.Viewer.Document = new FlowDocumentRenderer(fileName, extensions, darkMode).Render(source);
            state.FileName = fileName;
            state.Extensions = extensions;
            state.DarkMode = darkMode;
            state.ResetSearch();
            return true;
        }
        catch (Exception exception)
        {
            state.Viewer.Document = ErrorDocument(exception, darkMode);
            return false;
        }
    }

    public static void Focus(IntPtr handle)
    {
        if (Views.TryGetValue(handle, out var state))
            state.Viewer.Focus();
    }

    public static void SelectAll(IntPtr handle)
    {
        if (Views.TryGetValue(handle, out var state) && state.Viewer.Document is { } document)
            state.Viewer.Selection.Select(document.ContentStart, document.ContentEnd);
    }

    public static void Copy(IntPtr handle)
    {
        if (Views.TryGetValue(handle, out var state) && !state.Viewer.Selection.IsEmpty)
            ApplicationCommands.Copy.Execute(null, state.Viewer);
    }

    public static void Zoom(IntPtr handle, int delta)
    {
        if (!Views.TryGetValue(handle, out var state))
            return;
        state.Viewer.Zoom = delta == 0
            ? 100
            : Math.Max(state.Viewer.MinZoom, Math.Min(state.Viewer.MaxZoom, state.Viewer.Zoom + delta));
    }

    public static bool Find(IntPtr handle, string searchText, int searchFlags)
    {
        if (!Views.TryGetValue(handle, out var state) || state.Viewer.Document is null ||
            string.IsNullOrEmpty(searchText))
            return false;

        const int findFirst = 1;
        const int matchCase = 2;
        const int wholeWords = 4;
        const int backwards = 8;
        var buffer = BuildSearchBuffer(state.Viewer.Document);
        var comparison = (searchFlags & matchCase) != 0
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var sameSearch = state.SearchText == searchText &&
            state.SearchFlags == (searchFlags & (matchCase | wholeWords | backwards));
        if ((searchFlags & findFirst) != 0 || !sameSearch)
            state.ResetSearch();

        var reverse = (searchFlags & backwards) != 0;
        var start = reverse
            ? (state.LastMatchStart >= 0 ? state.LastMatchStart - 1 : buffer.Text.Length - searchText.Length)
            : (state.LastMatchEnd >= 0 ? state.LastMatchEnd : 0);
        var match = FindIndex(buffer.Text, searchText, start, reverse, comparison,
            (searchFlags & wholeWords) != 0);
        if (match < 0 && state.LastMatchStart >= 0)
        {
            start = reverse ? buffer.Text.Length - searchText.Length : 0;
            match = FindIndex(buffer.Text, searchText, start, reverse, comparison,
                (searchFlags & wholeWords) != 0);
        }
        if (match < 0 || match + searchText.Length >= buffer.Positions.Count)
            return false;

        state.Viewer.Selection.Select(buffer.Positions[match], buffer.Positions[match + searchText.Length]);
        state.Viewer.Focus();
        buffer.Positions[match].Paragraph?.BringIntoView();
        state.SearchText = searchText;
        state.SearchFlags = searchFlags & (matchCase | wholeWords | backwards);
        state.LastMatchStart = match;
        state.LastMatchEnd = match + searchText.Length;
        return true;
    }

    public static void Destroy(IntPtr handle)
    {
        if (!Views.TryGetValue(handle, out var state))
            return;
        Views.Remove(handle);
        state.Source.RootVisual = null;
        state.Source.Dispose();
    }

    private static FlowDocumentScrollViewer CreateViewer(bool darkMode)
    {
        var viewer = new FlowDocumentScrollViewer
        {
            IsToolBarVisible = false,
            IsSelectionEnabled = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = true,
            Zoom = 100,
            MinZoom = 50,
            MaxZoom = 250,
            ZoomIncrement = 10,
        };
        ApplyTheme(viewer, darkMode);
        return viewer;
    }

    private static void ApplyTheme(FlowDocumentScrollViewer viewer, bool darkMode)
    {
        viewer.Background = MarkdownTheme.Brush(darkMode
            ? MarkdownTheme.DarkBackground
            : MarkdownTheme.LightBackground);
        viewer.Foreground = MarkdownTheme.Brush(darkMode
            ? MarkdownTheme.DarkForeground
            : MarkdownTheme.LightForeground);
    }

    private static void HandlePreviewKey(ViewState state, KeyEventArgs args)
    {
        if (args.Key != Key.Escape)
            return;
        var listerWindow = GetParent(state.ParentWindow);
        if (listerWindow == IntPtr.Zero)
            listerWindow = state.ParentWindow;
        PostMessage(listerWindow, WmKeyDown, new IntPtr(VkEscape), IntPtr.Zero);
        PostMessage(listerWindow, WmKeyUp, new IntPtr(VkEscape), new IntPtr(unchecked((int)0xC0000000)));
        args.Handled = true;
    }

    private static FlowDocument ErrorDocument(Exception exception, bool darkMode)
    {
        var foreground = MarkdownTheme.Brush(darkMode ? "#FF9A94" : "#CF222E");
        var background = MarkdownTheme.Brush(darkMode
            ? MarkdownTheme.DarkBackground
            : MarkdownTheme.LightBackground);
        var document = new FlowDocument
        {
            PagePadding = new Thickness(20),
            Foreground = foreground,
            Background = background,
        };
        document.Blocks.Add(new Paragraph(new Bold(new Run("MarkdownView: ошибка отображения"))));
        document.Blocks.Add(new Paragraph(new Run(exception.Message)));
        return document;
    }

    private static SearchBuffer BuildSearchBuffer(FlowDocument document)
    {
        var text = new StringBuilder();
        var positions = new List<TextPointer>();
        var pointer = document.ContentStart;
        while (pointer is not null && pointer.CompareTo(document.ContentEnd) < 0)
        {
            if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var run = pointer.GetTextInRun(LogicalDirection.Forward);
                for (var index = 0; index < run.Length; index++)
                {
                    positions.Add(pointer.GetPositionAtOffset(index, LogicalDirection.Forward) ?? pointer);
                    text.Append(run[index]);
                }
                pointer = pointer.GetPositionAtOffset(run.Length, LogicalDirection.Forward)
                    ?? pointer.GetNextContextPosition(LogicalDirection.Forward);
                continue;
            }

            if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementEnd &&
                pointer.Parent is Paragraph)
            {
                positions.Add(pointer);
                text.Append('\n');
            }
            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }
        positions.Add(document.ContentEnd);
        return new SearchBuffer(text.ToString(), positions);
    }

    private static int FindIndex(string source, string value, int start, bool reverse,
        StringComparison comparison, bool wholeWords)
    {
        if (value.Length == 0 || source.Length < value.Length)
            return -1;
        start = Math.Max(0, Math.Min(source.Length - value.Length, start));
        while (true)
        {
            var index = reverse
                ? source.LastIndexOf(value, start, comparison)
                : source.IndexOf(value, start, comparison);
            if (index < 0)
                return -1;
            if (!wholeWords || IsWordBoundary(source, index - 1) &&
                IsWordBoundary(source, index + value.Length))
                return index;
            if (reverse)
            {
                if (index == 0)
                    return -1;
                start = index - 1;
            }
            else
            {
                start = index + 1;
                if (start > source.Length - value.Length)
                    return -1;
            }
        }
    }

    private static bool IsWordBoundary(string source, int index) =>
        index < 0 || index >= source.Length || !(char.IsLetterOrDigit(source[index]) || source[index] == '_');

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    private sealed class ViewState
    {
        internal ViewState(HwndSource source, FlowDocumentScrollViewer viewer, IntPtr parentWindow,
            string fileName, string extensions, bool darkMode)
        {
            Source = source;
            Viewer = viewer;
            ParentWindow = parentWindow;
            FileName = fileName;
            Extensions = extensions;
            DarkMode = darkMode;
        }

        internal HwndSource Source { get; }
        internal FlowDocumentScrollViewer Viewer { get; }
        internal IntPtr ParentWindow { get; }
        internal string FileName { get; set; }
        internal string Extensions { get; set; }
        internal bool DarkMode { get; set; }
        internal string? SearchText { get; set; }
        internal int SearchFlags { get; set; }
        internal int LastMatchStart { get; set; } = -1;
        internal int LastMatchEnd { get; set; } = -1;

        internal void ResetSearch()
        {
            SearchText = null;
            SearchFlags = 0;
            LastMatchStart = -1;
            LastMatchEnd = -1;
        }
    }

    private sealed class SearchBuffer
    {
        internal SearchBuffer(string text, List<TextPointer> positions)
        {
            Text = text;
            Positions = positions;
        }

        internal string Text { get; }
        internal List<TextPointer> Positions { get; }
    }
}
