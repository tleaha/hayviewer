using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using HayViewer.Core.Models;
using HayViewer.Core.Services;
using HayViewer.Highlighting;
using HayViewer.ViewModels;
using ICSharpCode.AvalonEdit.Folding;

namespace HayViewer;

public partial class JsonEditorView : UserControl
{
    private readonly JsonService _jsonService = new();
    private readonly SearchService _searchService = new();
    private readonly JsonTreeBuilder _treeBuilder = new();
    private readonly JsonHighlightingTransformer _highlighter = new();
    private readonly SearchHighlightRenderer _searchRenderer = new();
    private FoldingManager? _foldingManager;
    private List<SearchMatch> _searchMatches = new();
    private int _searchCurrentIndex = -1;
    private bool _suppressTextChange;

    public JsonEditorView()
    {
        InitializeComponent();
    }

    private EditorTabViewModel? Tab => DataContext as EditorTabViewModel;

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        // Set up AvalonEdit
        Editor.TextArea.TextView.LineTransformers.Add(_highlighter);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_searchRenderer);
        Editor.TextChanged += Editor_TextChanged;
        Editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;

        // Install code folding
        _foldingManager = FoldingManager.Install(Editor.TextArea);

        // Load content from view model
        if (Tab is { } tab)
        {
            _suppressTextChange = true;
            Editor.Text = tab.Content;
            _suppressTextChange = false;
            Editor.CaretOffset = Math.Min(tab.CaretOffset, Editor.Document.TextLength);
            ApplyTheme(tab);
            UpdateFolding();
        }

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (Tab is { } tab)
        {
            _suppressTextChange = true;
            Editor.Text = tab.Content;
            _suppressTextChange = false;
            Editor.CaretOffset = Math.Min(tab.CaretOffset, Editor.Document.TextLength);
            ApplyTheme(tab);
            UpdateFolding();
            RefreshTree();
        }
    }

    // Called by MainWindow when the theme changes.
    public void ApplyTheme(EditorTabViewModel? tab = null)
    {
        var mainVm = (Application.Current.MainWindow?.DataContext as MainViewModel);
        bool isDark = mainVm?.Settings.Theme == AppTheme.Dark;
        var colors = isDark ? SyntaxColors.Dark : SyntaxColors.Light;
        _highlighter.Colors = colors;
        Editor.Background = colors.Background;
        Editor.Foreground = colors.Default;
        Editor.LineNumbersForeground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x57, 0x60, 0x6A));
        Editor.TextArea.TextView.Redraw();
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (_suppressTextChange) return;
        if (Tab is { } tab)
        {
            tab.Content = Editor.Text;
            tab.IsDirty = true;
            UpdateStatusBar();
            UpdateFolding();
            if (_searchMatches.Count > 0) RunSearch();
        }
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        if (Tab is { } tab)
        {
            tab.CaretOffset = Editor.CaretOffset;
            UpdateStatusPosition();
        }
    }

    private void UpdateStatusBar()
    {
        if (Tab is null) return;
        UpdateStatusPosition();
        long byteSize = System.Text.Encoding.UTF8.GetByteCount(Editor.Text);
        Tab.StatusSize = byteSize < 1024 ? $"{byteSize} B"
            : byteSize < 1048576 ? $"{byteSize / 1024.0:F1} KB"
            : $"{byteSize / 1048576.0:F1} MB";
        var v = _jsonService.Validate(Editor.Text);
        if (v.IsSuccess)
        {
            Tab.StatusValidity = "Valid JSON";
            int nodes = _jsonService.CountNodes(Editor.Text);
            Tab.StatusNodes = $"{nodes:N0} nodes";
        }
        else
        {
            Tab.StatusValidity = $"Invalid: {v.Error}";
            Tab.StatusNodes = "";
        }
    }

    private void UpdateStatusPosition()
    {
        if (Tab is null) return;
        var loc = Editor.Document.GetLocation(Editor.CaretOffset);
        Tab.StatusPosition = $"Ln {loc.Line}, Col {loc.Column}";
    }

    public void RefreshTree()
    {
        if (Tab is null) return;
        var nodes = _treeBuilder.Build(Editor.Text);
        Tab.TreeNodes = nodes;
        JsonTree.ItemsSource = nodes;
    }

    private void UpdateFolding()
    {
        if (_foldingManager is null || string.IsNullOrEmpty(Editor.Text)) return;
        var folds = new List<NewFolding>();
        BuildFoldings(Editor.Text, folds);
        _foldingManager.UpdateFoldings(folds, -1);
    }

    private static void BuildFoldings(string text, List<NewFolding> folds)
    {
        var stack = new Stack<(int offset, char open)>();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '{' || c == '[')
            {
                stack.Push((i, c));
            }
            else if ((c == '}' || c == ']') && stack.Count > 0)
            {
                var (startOff, _) = stack.Pop();
                if (i > startOff + 1)
                    folds.Add(new NewFolding(startOff, i + 1));
            }
        }
        folds.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
    }

    // ─── Search ──────────────────────────────────────────────────────────────

    public void ShowSearch()
    {
        SearchBar.Visibility = Visibility.Visible;
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    public void HideSearch()
    {
        SearchBar.Visibility = Visibility.Collapsed;
        _searchRenderer.Clear();
        Editor.TextArea.TextView.Redraw();
        _searchMatches.Clear();
        _searchCurrentIndex = -1;
        MatchCounter.Text = "No results";
    }

    private void RunSearch()
    {
        string query = SearchBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            _searchRenderer.Clear();
            Editor.TextArea.TextView.Redraw();
            _searchMatches.Clear();
            _searchCurrentIndex = -1;
            MatchCounter.Text = "No results";
            return;
        }

        var scope = ScopeCombo.SelectedIndex switch { 1 => SearchScope.Keys, 2 => SearchScope.Values, _ => SearchScope.Both };
        bool cs = CaseSensitiveBtn.IsChecked == true;
        bool rx = RegexBtn.IsChecked == true;

        _searchMatches = _searchService.Search(Editor.Text, query, scope, cs, rx);
        _searchCurrentIndex = _searchMatches.Count > 0 ? 0 : -1;
        UpdateSearchUI();
    }

    private void UpdateSearchUI()
    {
        _searchRenderer.SetMatches(_searchMatches, _searchCurrentIndex);
        Editor.TextArea.TextView.Redraw();
        if (_searchMatches.Count == 0)
        {
            MatchCounter.Text = "No results";
            return;
        }
        MatchCounter.Text = $"{_searchCurrentIndex + 1} of {_searchMatches.Count}";
        if (_searchCurrentIndex >= 0)
            ScrollToMatch(_searchMatches[_searchCurrentIndex]);
    }

    private void ScrollToMatch(SearchMatch match)
    {
        Editor.CaretOffset = match.Offset;
        Editor.Select(match.Offset, match.Length);
        Editor.ScrollToLine(match.Line);
    }

    public void GoToNextMatch()
    {
        if (_searchMatches.Count == 0) return;
        _searchCurrentIndex = (_searchCurrentIndex + 1) % _searchMatches.Count;
        UpdateSearchUI();
    }

    public void GoToPrevMatch()
    {
        if (_searchMatches.Count == 0) return;
        _searchCurrentIndex = (_searchCurrentIndex - 1 + _searchMatches.Count) % _searchMatches.Count;
        UpdateSearchUI();
    }

    // ─── Tree view ────────────────────────────────────────────────────────────

    private void JsonTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is JsonNodeModel node && node.TextOffset >= 0 &&
            node.TextOffset <= Editor.Document.TextLength)
        {
            Editor.CaretOffset = node.TextOffset;
            Editor.ScrollToLine(node.Line);
        }
    }

    // ─── UI event handlers ────────────────────────────────────────────────────

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { HideSearch(); e.Handled = true; }
        else if (e.Key == Key.Enter)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) GoToPrevMatch();
            else GoToNextMatch();
            e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) GoToPrevMatch();
            else GoToNextMatch();
            e.Handled = true;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RunSearch();

    private void ScopeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RunSearch();

    private void SearchOptions_Changed(object sender, RoutedEventArgs e) => RunSearch();

    private void CloseSearch_Click(object sender, RoutedEventArgs e) => HideSearch();

    private void Splitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        // Persist tree column width to settings via main window
        if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
            vm.Settings.TreeViewWidth = TreeColumn.ActualWidth;
    }

    private void UserControl_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            // Bubble up to MainWindow for handling
            var mw = Application.Current.MainWindow as MainWindow;
            mw?.OpenFileFromDrop(files[0]);
        }
    }

    // ─── Public helpers called from MainWindow ────────────────────────────────

    public void SetText(string text)
    {
        _suppressTextChange = true;
        Editor.Text = text;
        _suppressTextChange = false;
        UpdateStatusBar();
        UpdateFolding();
        RefreshTree();
        Editor.TextArea.TextView.Redraw();
    }

    public string GetText() => Editor.Text;

    public bool IsWordWrap
    {
        get => Editor.WordWrap;
        set => Editor.WordWrap = value;
    }

    // Scroll to a specific line (1-based).
    public void ScrollToLine(int line) => Editor.ScrollToLine(line);

    // Highlight the given line as the error line.
    public void HighlightErrorLine(int line)
    {
        if (line < 1 || line > Editor.Document.LineCount) return;
        var docLine = Editor.Document.GetLineByNumber(line);
        Editor.CaretOffset = docLine.Offset;
        Editor.Select(docLine.Offset, docLine.Length);
        Editor.ScrollToLine(line);
    }
}
