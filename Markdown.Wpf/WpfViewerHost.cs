using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Text;
using System.Windows.Threading;

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
    private static readonly IReadOnlyDictionary<string, FrameworkContentElement> EmptyAnchors =
        new Dictionary<string, FrameworkContentElement>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MarkdownFileExtensions = new(
        new[] { "md", "markdown", "mdown", "mdtext", "mdtxt", "mdwn", "mk", "mkd", "mkdn", "mkdown" },
        StringComparer.OrdinalIgnoreCase);

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
        var root = CreateViewerRoot(viewer, darkMode, out var navigationBar,
            out var backButton, out var forwardButton);
        var state = new ViewState(source, root, viewer, navigationBar, backButton, forwardButton,
            parentWindow, fileName, extensions, darkMode);
        source.RootVisual = root;
        viewer.PreviewKeyDown += (_, args) => HandlePreviewKey(state, args);
        backButton.Click += (_, _) => NavigateBack(state);
        forwardButton.Click += (_, _) => NavigateForward(state);
        Views[source.Handle] = state;
        Load(source.Handle, fileName, extensions, darkMode);
        return source.Handle;
    }

    public static bool Load(IntPtr handle, string fileName, string extensions, bool darkMode)
    {
        if (!Views.TryGetValue(handle, out var state))
            return false;
        return LoadDocument(state, fileName, extensions, darkMode, true, null, null, true);
    }

    private static bool LoadDocument(ViewState state, string fileName, string extensions,
        bool darkMode, bool resetNavigation, double? requestedOffset, string? requestedAnchor,
        bool showErrors)
    {
        var previousFileName = state.FileName;
        var previousExtensions = state.Extensions;
        var previousDarkMode = state.DarkMode;
        var previousSourceText = state.LastSourceText;
        var previousAnchors = state.Anchors;
        var previousLoadedState = state.HasLoadedDocument;
        state.StopWatching();
        try
        {
            var fullFileName = Path.GetFullPath(fileName);
            var isNewDocument = !state.HasLoadedDocument ||
                !string.Equals(state.FileName, fullFileName, StringComparison.OrdinalIgnoreCase);
            if (state.HasLoadedDocument && isNewDocument)
                state.SaveScrollPosition();

            state.FileName = fullFileName;
            state.Extensions = extensions;
            state.DarkMode = darkMode;
            state.LastSourceText = null;
            state.LoadGeneration++;
            state.ConfigureWatcher();
            var source = ReadSourceFile(state.FileName);
            var renderer = new FlowDocumentRenderer(state.FileName, extensions, darkMode,
                url => HandleLink(state, url));
            ApplyTheme(state.Viewer, darkMode);
            ApplyNavigationTheme(state, darkMode);
            state.Viewer.Document = renderer.Render(source);
            state.Anchors = renderer.Anchors;
            state.LastSourceText = source;
            state.HasLoadedDocument = true;
            state.ResetSearch();
            if (resetNavigation)
                state.ResetNavigation();
            UpdateNavigationControls(state);
            if (!string.IsNullOrEmpty(requestedAnchor))
            {
                if (!ScrollToAnchor(state, requestedAnchor!))
                    RestoreScrollOffset(state, 0, state.LoadGeneration);
            }
            else if (requestedOffset.HasValue)
            {
                RestoreScrollOffset(state, requestedOffset.Value, state.LoadGeneration);
            }
            else if (isNewDocument)
            {
                RestoreScrollPosition(state, state.LoadGeneration);
            }
            return true;
        }
        catch (Exception exception)
        {
            if (!showErrors)
            {
                state.FileName = previousFileName;
                state.Extensions = previousExtensions;
                state.DarkMode = previousDarkMode;
                state.LastSourceText = previousSourceText;
                state.Anchors = previousAnchors;
                state.HasLoadedDocument = previousLoadedState;
                ApplyTheme(state.Viewer, previousDarkMode);
                ApplyNavigationTheme(state, previousDarkMode);
                if (previousLoadedState)
                    state.ConfigureWatcher();
                return false;
            }
            state.HasLoadedDocument = false;
            state.Viewer.Document = ErrorDocument(exception, darkMode);
            state.Anchors = EmptyAnchors;
            if (resetNavigation)
                state.ResetNavigation();
            UpdateNavigationControls(state);
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
        if (!Views.TryGetValue(handle, out var state))
            return;
        if (FindFocusedCodeBox(state.Viewer) is { } codeBox)
        {
            codeBox.Selection.Select(codeBox.Document.ContentStart, codeBox.Document.ContentEnd);
            return;
        }
        if (state.Viewer.Document is { } document)
            state.Viewer.Selection.Select(document.ContentStart, document.ContentEnd);
    }

    public static void Copy(IntPtr handle)
    {
        if (!Views.TryGetValue(handle, out var state))
            return;
        if (FindFocusedCodeBox(state.Viewer) is { } codeBox && !codeBox.Selection.IsEmpty)
        {
            ApplicationCommands.Copy.Execute(null, codeBox);
            return;
        }
        if (!state.Viewer.Selection.IsEmpty)
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
        state.Dispose();
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

    private static Grid CreateViewerRoot(FlowDocumentScrollViewer viewer, bool darkMode,
        out Border navigationBar, out Button backButton, out Button forwardButton)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        backButton = CreateNavigationButton("← Назад", "Предыдущий документ — Alt+Left");
        forwardButton = CreateNavigationButton("Вперёд →", "Следующий документ — Alt+Right");
        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 5, 8, 5),
        };
        controls.Children.Add(backButton);
        controls.Children.Add(forwardButton);

        navigationBar = new Border
        {
            Child = controls,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Visibility = Visibility.Collapsed,
        };
        Grid.SetRow(navigationBar, 0);
        Grid.SetRow(viewer, 1);
        root.Children.Add(navigationBar);
        root.Children.Add(viewer);

        ApplyNavigationTheme(root, navigationBar, backButton, forwardButton, darkMode);
        return root;
    }

    private static Button CreateNavigationButton(string caption, string toolTip) => new()
    {
        Content = caption,
        ToolTip = toolTip,
        MinWidth = 82,
        Height = 28,
        Margin = new Thickness(0, 0, 6, 0),
        Padding = new Thickness(8, 1, 8, 2),
        Focusable = false,
        IsTabStop = false,
        Cursor = Cursors.Hand,
    };

    private static void ApplyNavigationTheme(ViewState state, bool darkMode) =>
        ApplyNavigationTheme(state.Root, state.NavigationBar, state.BackButton,
            state.ForwardButton, darkMode);

    private static void ApplyNavigationTheme(Grid root, Border navigationBar, Button backButton,
        Button forwardButton, bool darkMode)
    {
        var background = MarkdownTheme.Brush(darkMode
            ? MarkdownTheme.DarkBackground
            : MarkdownTheme.LightBackground);
        var panel = MarkdownTheme.Brush(darkMode ? MarkdownTheme.DarkPanel : MarkdownTheme.LightPanel);
        var foreground = MarkdownTheme.Brush(darkMode
            ? MarkdownTheme.DarkForeground
            : MarkdownTheme.LightForeground);
        var border = MarkdownTheme.Brush(darkMode ? MarkdownTheme.DarkBorder : MarkdownTheme.LightBorder);
        root.Background = background;
        navigationBar.Background = panel;
        navigationBar.BorderBrush = border;
        foreach (var button in new[] { backButton, forwardButton })
        {
            button.Background = background;
            button.Foreground = foreground;
            button.BorderBrush = border;
        }
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
        var key = args.Key == Key.System ? args.SystemKey : args.Key;
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0 && key == Key.Left)
        {
            if (NavigateBack(state))
                args.Handled = true;
            return;
        }
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0 && key == Key.Right)
        {
            if (NavigateForward(state))
                args.Handled = true;
            return;
        }
        if (key != Key.Escape)
            return;
        var listerWindow = GetParent(state.ParentWindow);
        if (listerWindow == IntPtr.Zero)
            listerWindow = state.ParentWindow;
        PostMessage(listerWindow, WmKeyDown, new IntPtr(VkEscape), IntPtr.Zero);
        PostMessage(listerWindow, WmKeyUp, new IntPtr(VkEscape), new IntPtr(unchecked((int)0xC0000000)));
        args.Handled = true;
    }

    private static bool HandleLink(ViewState state, string url)
    {
        if (state.IsDisposed || string.IsNullOrWhiteSpace(url))
            return false;

        if (url[0] == '#')
        {
            NavigateToAnchorLink(state, url.Substring(1));
            return true;
        }
        if (Uri.TryCreate(url, UriKind.Absolute, out _) || Path.IsPathRooted(url))
            return false;

        var separator = url.IndexOf('#');
        var linkPath = separator >= 0 ? url.Substring(0, separator) : url;
        var anchor = separator >= 0 ? url.Substring(separator + 1) : null;
        try
        {
            linkPath = Uri.UnescapeDataString(linkPath).Replace('/', Path.DirectorySeparatorChar);
        }
        catch (UriFormatException)
        {
            return false;
        }
        if (!IsMarkdownPath(linkPath))
            return false;

        var directory = Path.GetDirectoryName(state.FileName) ?? Environment.CurrentDirectory;
        string targetFile;
        try
        {
            targetFile = Path.GetFullPath(Path.Combine(directory, linkPath));
        }
        catch (Exception)
        {
            return true;
        }
        if (!File.Exists(targetFile))
            return true;

        var current = CaptureLocation(state);
        if (string.Equals(targetFile, state.FileName, StringComparison.OrdinalIgnoreCase))
        {
            var navigated = string.IsNullOrEmpty(anchor)
                ? RestoreScrollOffset(state, 0, state.LoadGeneration)
                : ScrollToAnchor(state, anchor!);
            if (navigated)
                RecordForwardNavigation(state, current);
            return true;
        }

        var targetOffset = string.IsNullOrEmpty(anchor) ? 0.0 : (double?)null;
        if (LoadDocument(state, targetFile, state.Extensions, state.DarkMode, false,
                targetOffset, anchor, false))
        {
            RecordForwardNavigation(state, current);
            UpdateListerDocumentTitle(state);
        }
        return true;
    }

    private static bool IsMarkdownPath(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        return extension.Length > 0 && MarkdownFileExtensions.Contains(extension);
    }

    private static void NavigateToAnchorLink(ViewState state, string anchor)
    {
        var current = CaptureLocation(state);
        if (ScrollToAnchor(state, anchor))
            RecordForwardNavigation(state, current);
    }

    private static void RecordForwardNavigation(ViewState state, NavigationLocation current)
    {
        state.BackHistory.Push(current);
        state.ForwardHistory.Clear();
        UpdateNavigationControls(state);
    }

    private static bool NavigateBack(ViewState state)
    {
        if (state.BackHistory.Count == 0 || !state.HasLoadedDocument)
            return false;
        var target = state.BackHistory.Peek();
        var current = CaptureLocation(state);
        if (!NavigateToLocation(state, target))
            return false;
        state.BackHistory.Pop();
        state.ForwardHistory.Push(current);
        UpdateNavigationControls(state);
        return true;
    }

    private static bool NavigateForward(ViewState state)
    {
        if (state.ForwardHistory.Count == 0 || !state.HasLoadedDocument)
            return false;
        var target = state.ForwardHistory.Peek();
        var current = CaptureLocation(state);
        if (!NavigateToLocation(state, target))
            return false;
        state.ForwardHistory.Pop();
        state.BackHistory.Push(current);
        UpdateNavigationControls(state);
        return true;
    }

    private static bool NavigateToLocation(ViewState state, NavigationLocation target)
    {
        if (string.Equals(state.FileName, target.FileName, StringComparison.OrdinalIgnoreCase))
            return RestoreScrollOffset(state, target.VerticalOffset, state.LoadGeneration);
        if (!File.Exists(target.FileName) ||
            !LoadDocument(state, target.FileName, state.Extensions, state.DarkMode, false,
                target.VerticalOffset, null, false))
            return false;
        UpdateListerDocumentTitle(state);
        return true;
    }

    private static NavigationLocation CaptureLocation(ViewState state)
    {
        var offset = FindVisualChild<ScrollViewer>(state.Viewer)?.VerticalOffset ?? 0;
        return new NavigationLocation(state.FileName, offset);
    }

    private static bool ScrollToAnchor(ViewState state, string encodedAnchor)
    {
        string anchor;
        try
        {
            anchor = Uri.UnescapeDataString(encodedAnchor.TrimStart('#'));
        }
        catch (UriFormatException)
        {
            return false;
        }
        if (anchor.Length == 0)
            return RestoreScrollOffset(state, 0, state.LoadGeneration);
        if (!state.Anchors.TryGetValue(anchor, out var target))
            return false;

        var generation = state.LoadGeneration;
        state.Viewer.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (state.IsDisposed || state.LoadGeneration != generation)
                return;
            target.BringIntoView();
            state.Viewer.Focus();
        }));
        return true;
    }

    private static void UpdateNavigationControls(ViewState state)
    {
        var canGoBack = state.BackHistory.Count > 0;
        var canGoForward = state.ForwardHistory.Count > 0;
        state.BackButton.IsEnabled = canGoBack;
        state.ForwardButton.IsEnabled = canGoForward;
        state.NavigationBar.Visibility = canGoBack || canGoForward
            ? Visibility.Visible
            : Visibility.Collapsed;
        state.BackButton.ToolTip = canGoBack
            ? $"Предыдущий документ: {Path.GetFileName(state.BackHistory.Peek().FileName)} — Alt+Left"
            : "Предыдущий документ — Alt+Left";
        state.ForwardButton.ToolTip = canGoForward
            ? $"Следующий документ: {Path.GetFileName(state.ForwardHistory.Peek().FileName)} — Alt+Right"
            : "Следующий документ — Alt+Right";
    }

    private static RichTextBox? FindFocusedCodeBox(FlowDocumentScrollViewer viewer)
    {
        var current = Keyboard.FocusedElement as DependencyObject;
        RichTextBox? candidate = null;
        while (current is not null)
        {
            if (current is RichTextBox codeBox && codeBox.IsReadOnly)
                candidate = codeBox;
            if (ReferenceEquals(current, viewer))
                return candidate;
            current = current is Visual || current is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    private static string ReadSourceFile(string fileName)
    {
        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    private static void ReloadChangedFile(ViewState state)
    {
        if (state.IsDisposed)
            return;
        try
        {
            var source = ReadSourceFile(state.FileName);
            if (string.Equals(source, state.LastSourceText, StringComparison.Ordinal))
            {
                state.ReloadAttempts = 0;
                return;
            }

            var restoreSavedPosition = !state.HasLoadedDocument;
            var scrollViewer = FindVisualChild<ScrollViewer>(state.Viewer);
            var verticalOffset = scrollViewer?.VerticalOffset ?? 0;
            var renderer = new FlowDocumentRenderer(
                state.FileName, state.Extensions, state.DarkMode, url => HandleLink(state, url));
            ApplyTheme(state.Viewer, state.DarkMode);
            state.Viewer.Document = renderer.Render(source);
            state.Anchors = renderer.Anchors;
            state.LastSourceText = source;
            state.HasLoadedDocument = true;
            state.ResetSearch();
            state.ReloadAttempts = 0;

            if (restoreSavedPosition)
            {
                RestoreScrollPosition(state, state.LoadGeneration);
            }
            else if (scrollViewer is not null && verticalOffset > 0)
            {
                state.Viewer.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    if (!state.IsDisposed)
                        scrollViewer.ScrollToVerticalOffset(verticalOffset);
                }));
            }
        }
        catch (IOException)
        {
            state.RetryReload();
        }
        catch (UnauthorizedAccessException)
        {
            state.RetryReload();
        }
        catch (Exception)
        {
            // Keep the last successfully rendered document for unexpected transient failures.
            state.ReloadAttempts = 0;
        }
    }

    private static void RestoreScrollPosition(ViewState state, int loadGeneration)
    {
        var savedOffset = ScrollPositionStore.Load(state.FileName);
        if (!savedOffset.HasValue || savedOffset.Value <= 0)
            return;

        RestoreScrollOffset(state, savedOffset.Value, loadGeneration);
    }

    private static bool RestoreScrollOffset(ViewState state, double offset, int loadGeneration)
    {
        if (double.IsNaN(offset) || double.IsInfinity(offset) || offset < 0)
            return false;

        state.Viewer.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (state.IsDisposed || state.LoadGeneration != loadGeneration)
                return;
            state.Viewer.UpdateLayout();
            FindVisualChild<ScrollViewer>(state.Viewer)?.ScrollToVerticalOffset(offset);
        }));
        return true;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                return match;
            if (FindVisualChild<T>(child) is { } nested)
                return nested;
        }
        return null;
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

    private static void UpdateListerDocumentTitle(ViewState state)
    {
        var listerWindow = GetParent(state.ParentWindow);
        if (listerWindow == IntPtr.Zero)
            return;
        var length = GetWindowTextLength(listerWindow);
        if (length <= 0)
            return;
        var buffer = new StringBuilder(length + 1);
        if (GetWindowText(listerWindow, buffer, buffer.Capacity) <= 0)
            return;
        var title = buffer.ToString();
        var pathMarker = title.IndexOf(" - [", StringComparison.Ordinal);
        if (pathMarker < 0 || !title.EndsWith("]", StringComparison.Ordinal))
            return;
        SetWindowText(listerWindow, title.Substring(0, pathMarker) + " - [" + state.FileName + "]");
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximumCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowText(IntPtr window, string text);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    private sealed class ViewState : IDisposable
    {
        private static readonly TimeSpan ReloadDelay = TimeSpan.FromMilliseconds(350);
        private const int MaxReloadAttempts = 20;
        private FileSystemWatcher? _watcher;
        private readonly DispatcherTimer _reloadTimer;

        internal ViewState(HwndSource source, Grid root, FlowDocumentScrollViewer viewer,
            Border navigationBar, Button backButton, Button forwardButton, IntPtr parentWindow,
            string fileName, string extensions, bool darkMode)
        {
            Source = source;
            Root = root;
            Viewer = viewer;
            NavigationBar = navigationBar;
            BackButton = backButton;
            ForwardButton = forwardButton;
            ParentWindow = parentWindow;
            FileName = fileName;
            Extensions = extensions;
            DarkMode = darkMode;
            _reloadTimer = new DispatcherTimer(DispatcherPriority.Background, viewer.Dispatcher)
            {
                Interval = ReloadDelay,
            };
            _reloadTimer.Tick += (_, _) =>
            {
                _reloadTimer.Stop();
                ReloadChangedFile(this);
            };
        }

        internal HwndSource Source { get; }
        internal Grid Root { get; }
        internal FlowDocumentScrollViewer Viewer { get; }
        internal Border NavigationBar { get; }
        internal Button BackButton { get; }
        internal Button ForwardButton { get; }
        internal IntPtr ParentWindow { get; }
        internal string FileName { get; set; }
        internal string Extensions { get; set; }
        internal bool DarkMode { get; set; }
        internal string? SearchText { get; set; }
        internal int SearchFlags { get; set; }
        internal int LastMatchStart { get; set; } = -1;
        internal int LastMatchEnd { get; set; } = -1;
        internal string? LastSourceText { get; set; }
        internal int ReloadAttempts { get; set; }
        internal int LoadGeneration { get; set; }
        internal bool HasLoadedDocument { get; set; }
        internal bool IsDisposed { get; private set; }
        internal IReadOnlyDictionary<string, FrameworkContentElement> Anchors { get; set; } =
            EmptyAnchors;
        internal Stack<NavigationLocation> BackHistory { get; } = new();
        internal Stack<NavigationLocation> ForwardHistory { get; } = new();

        internal void ConfigureWatcher()
        {
            StopWatching();
            if (IsDisposed)
                return;

            var directory = Path.GetDirectoryName(FileName);
            var leafName = Path.GetFileName(FileName);
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(leafName) ||
                !Directory.Exists(directory))
                return;

            FileSystemWatcher? watcher = null;
            try
            {
                watcher = new FileSystemWatcher(directory, leafName)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime |
                        NotifyFilters.LastWrite | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                };
                watcher.Changed += (_, _) => ScheduleReload();
                watcher.Created += (_, _) => ScheduleReload();
                watcher.Deleted += (_, _) => ScheduleReload();
                watcher.Renamed += (_, _) => ScheduleReload();
                watcher.Error += (_, _) => ScheduleReload();
                watcher.EnableRaisingEvents = true;
                _watcher = watcher;
            }
            catch (IOException)
            {
                watcher?.Dispose();
            }
            catch (UnauthorizedAccessException)
            {
                watcher?.Dispose();
            }
            catch (ArgumentException)
            {
                watcher?.Dispose();
            }
        }

        private void ScheduleReload()
        {
            if (IsDisposed)
                return;
            try
            {
                Viewer.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (IsDisposed)
                        return;
                    ReloadAttempts = 0;
                    _reloadTimer.Stop();
                    _reloadTimer.Interval = ReloadDelay;
                    _reloadTimer.Start();
                }));
            }
            catch (InvalidOperationException)
            {
                // The WPF dispatcher is already shutting down with the Lister window.
            }
        }

        internal void RetryReload()
        {
            if (IsDisposed || ++ReloadAttempts >= MaxReloadAttempts)
                return;
            _reloadTimer.Stop();
            _reloadTimer.Interval = ReloadDelay;
            _reloadTimer.Start();
        }

        internal void StopWatching()
        {
            _reloadTimer.Stop();
            _watcher?.Dispose();
            _watcher = null;
        }

        internal void ResetSearch()
        {
            SearchText = null;
            SearchFlags = 0;
            LastMatchStart = -1;
            LastMatchEnd = -1;
        }

        internal void ResetNavigation()
        {
            BackHistory.Clear();
            ForwardHistory.Clear();
        }

        internal void SaveScrollPosition()
        {
            if (!HasLoadedDocument || string.IsNullOrEmpty(FileName))
                return;
            var verticalOffset = FindVisualChild<ScrollViewer>(Viewer)?.VerticalOffset ?? 0;
            ScrollPositionStore.Save(FileName, verticalOffset);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            SaveScrollPosition();
            IsDisposed = true;
            StopWatching();
        }
    }

    private sealed class NavigationLocation
    {
        internal NavigationLocation(string fileName, double verticalOffset)
        {
            FileName = fileName;
            VerticalOffset = verticalOffset;
        }

        internal string FileName { get; }
        internal double VerticalOffset { get; }
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
