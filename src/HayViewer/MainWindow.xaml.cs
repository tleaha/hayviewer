using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HayViewer.Core.Models;
using HayViewer.Core.Services;
using HayViewer.ViewModels;
using HayViewer.Views;
using Microsoft.Win32;

namespace HayViewer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly JsonService _jsonService = new();

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        WireCommands();
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    private void WireCommands()
    {
        _vm.OpenCommand = new RelayCommand(_ => OpenFile());
        _vm.SaveCommand = new RelayCommand(_ => SaveFile(), _ => _vm.SelectedTab is not null);
        _vm.SaveAsCommand = new RelayCommand(_ => SaveFileAs(), _ => _vm.SelectedTab is not null);
        _vm.NewTabCommand = new RelayCommand(_ => OpenNewTab());
        _vm.FormatCommand = new RelayCommand(_ => FormatJson(), _ => _vm.SelectedTab is not null);
        _vm.MinifyCommand = new RelayCommand(_ => MinifyJson(), _ => _vm.SelectedTab is not null);
        _vm.ValidateCommand = new RelayCommand(_ => ValidateJson(), _ => _vm.SelectedTab is not null);
        _vm.FindCommand = new RelayCommand(_ => ShowSearch(), _ => _vm.SelectedTab is not null);
        _vm.FindNextCommand = new RelayCommand(_ => ActiveEditor()?.GoToNextMatch());
        _vm.FindPrevCommand = new RelayCommand(_ => ActiveEditor()?.GoToPrevMatch());
        _vm.RefreshTreeCommand = new RelayCommand(_ => ActiveEditor()?.RefreshTree(), _ => _vm.SelectedTab is not null);
        _vm.ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
        _vm.NewWindowCommand = new RelayCommand(_ => OpenNewWindow());
        _vm.ExitCommand = new RelayCommand(_ => Close());
        _vm.AboutCommand = new RelayCommand(_ => ShowAbout());
        _vm.SetIndentCommand = new RelayCommand(p => SetIndent(p as string));
    }

    // ─── Window lifecycle ─────────────────────────────────────────────────────

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplySettings();
        RebuildRecentFilesMenu();
        // Open blank tab if none
        if (_vm.Tabs.Count == 0) _vm.OpenNewTab();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Check for unsaved changes
        foreach (var tab in _vm.Tabs)
        {
            if (tab.IsDirty)
            {
                var result = MessageBox.Show(
                    $"'{tab.FileName}' has unsaved changes. Save before closing?",
                    "Hay Viewer", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Cancel) { e.Cancel = true; return; }
                if (result == MessageBoxResult.Yes)
                {
                    _vm.SelectedTab = tab;
                    if (!SaveFile()) { e.Cancel = true; return; }
                }
            }
        }

        // Persist window state
        _vm.Settings.WindowWidth = Width;
        _vm.Settings.WindowHeight = Height;
        _vm.Settings.WindowLeft = Left;
        _vm.Settings.WindowTop = Top;
        _vm.Settings.IsMaximized = WindowState == WindowState.Maximized;
        _vm.SaveSettings();
    }

    private void ApplySettings()
    {
        var s = _vm.Settings;
        Width = s.WindowWidth;
        Height = s.WindowHeight;
        if (!double.IsNaN(s.WindowLeft) && !double.IsNaN(s.WindowTop))
        {
            Left = s.WindowLeft;
            Top = s.WindowTop;
        }
        if (s.IsMaximized) WindowState = WindowState.Maximized;
        IndentCombo.SelectedIndex = s.IndentStyle switch
        {
            IndentStyle.FourSpaces => 1,
            IndentStyle.Tab => 2,
            _ => 0
        };
        WordWrapMenu.IsChecked = s.WordWrap;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        bool isDark = _vm.Settings.Theme == AppTheme.Dark;
        ThemeBtn.Content = isDark ? "Light Theme" : "Dark Theme";

        // Apply dark/light background to window chrome
        var bg = isDark
            ? System.Windows.Media.Color.FromRgb(0x16, 0x1B, 0x22)
            : System.Windows.Media.Colors.White;
        Background = new System.Windows.Media.SolidColorBrush(bg);

        // Refresh all open editors
        foreach (var editor in GetAllEditorViews())
            editor.ApplyTheme();
    }

    // ─── File operations ─────────────────────────────────────────────────────

    private void OpenFile(string? path = null)
    {
        if (path is null)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Open JSON File"
            };
            if (dlg.ShowDialog(this) != true) return;
            path = dlg.FileName;
        }

        // If already open, switch to that tab
        if (_vm.TryFindExistingTab(path, out var existing))
        {
            _vm.SelectedTab = existing;
            return;
        }

        OpenFileFromPath(path);
    }

    private void OpenFileFromPath(string path)
    {
        try
        {
            string content = File.ReadAllText(path);
            var tab = _vm.OpenNewTab(path, content);
            _vm.AddRecentFile(path);
            RebuildRecentFilesMenu();
            UpdateWindowTitle(tab);

            // Refresh editor contents (editor view may not yet be loaded)
            Dispatcher.InvokeAsync(() =>
            {
                if (ActiveEditor() is { } editor)
                {
                    editor.SetText(content);
                    UpdateStatusBar(tab);
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open file:\n{ex.Message}", "Hay Viewer",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void OpenFileFromDrop(string path)
    {
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || File.Exists(path))
            OpenFile(path);
    }

    private bool SaveFile()
    {
        var tab = _vm.SelectedTab;
        if (tab is null) return false;
        if (tab.IsNew) return SaveFileAs();

        try
        {
            string text = ActiveEditor()?.GetText() ?? tab.Content;
            File.WriteAllText(tab.FilePath, text);
            tab.IsDirty = false;
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save file:\n{ex.Message}", "Hay Viewer",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool SaveFileAs()
    {
        var tab = _vm.SelectedTab;
        if (tab is null) return false;

        var dlg = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Save JSON File As",
            FileName = tab.FileName
        };
        if (dlg.ShowDialog(this) != true) return false;

        try
        {
            string text = ActiveEditor()?.GetText() ?? tab.Content;
            File.WriteAllText(dlg.FileName, text);
            tab.FilePath = dlg.FileName;
            tab.IsDirty = false;
            _vm.AddRecentFile(dlg.FileName);
            RebuildRecentFilesMenu();
            UpdateWindowTitle(tab);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save file:\n{ex.Message}", "Hay Viewer",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void OpenNewTab()
    {
        _vm.OpenNewTab();
    }

    // ─── JSON operations ─────────────────────────────────────────────────────

    private void FormatJson()
    {
        var editor = ActiveEditor();
        if (editor is null) return;
        var result = _jsonService.Format(editor.GetText(), _vm.Settings.IndentStyle);
        if (result.IsSuccess)
        {
            editor.SetText(result.Text);
            _vm.SelectedTab!.IsDirty = true;
            SetStatus("Formatted.", true);
            editor.RefreshTree();
        }
        else
        {
            ShowError(result);
        }
    }

    private void MinifyJson()
    {
        var editor = ActiveEditor();
        if (editor is null) return;
        var result = _jsonService.Minify(editor.GetText());
        if (result.IsSuccess)
        {
            editor.SetText(result.Text);
            _vm.SelectedTab!.IsDirty = true;
            SetStatus("Minified.", true);
        }
        else
        {
            ShowError(result);
        }
    }

    private void ValidateJson()
    {
        var editor = ActiveEditor();
        if (editor is null) return;
        var result = _jsonService.Validate(editor.GetText());
        if (result.IsSuccess)
        {
            SetStatus("Valid JSON.", true);
            MessageBox.Show("JSON is valid.", "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            ShowError(result);
            if (result.ErrorLine.HasValue)
                editor.HighlightErrorLine((int)result.ErrorLine.Value);
        }
    }

    private void ShowError(JsonOperationResult result)
    {
        string location = result.ErrorLine.HasValue
            ? $" (line {result.ErrorLine}, col {result.ErrorColumn})"
            : "";
        string msg = $"JSON error{location}: {result.Error}";
        SetStatus(msg, false);
        StatusValid.Foreground = System.Windows.Media.Brushes.Red;
    }

    // ─── Search ───────────────────────────────────────────────────────────────

    private void ShowSearch()
    {
        ActiveEditor()?.ShowSearch();
    }

    // ─── UI helpers ───────────────────────────────────────────────────────────

    private JsonEditorView? ActiveEditor()
    {
        if (MainTabControl.SelectedItem is not EditorTabViewModel) return null;
        // Walk the visual tree to find the JsonEditorView for the selected tab.
        return FindVisualChild<JsonEditorView>(MainTabControl);
    }

    private static T? FindVisualChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T target) return target;
            var found = FindVisualChild<T>(child);
            if (found is not null) return found;
        }
        return null;
    }

    private IEnumerable<JsonEditorView> GetAllEditorViews()
    {
        return FindVisualChildren<JsonEditorView>(MainTabControl);
    }

    private static IEnumerable<T> FindVisualChildren<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t) yield return t;
            foreach (var desc in FindVisualChildren<T>(child)) yield return desc;
        }
    }

    private void SetStatus(string message, bool valid)
    {
        StatusValid.Text = message;
        StatusValid.Foreground = valid
            ? System.Windows.Media.Brushes.Green
            : System.Windows.Media.Brushes.Red;
    }

    private void UpdateStatusBar(EditorTabViewModel? tab)
    {
        if (tab is null) return;
        StatusPosition.Text = tab.StatusPosition;
        StatusSize.Text = tab.StatusSize;
        StatusValid.Text = tab.StatusValidity;
        StatusValid.Foreground = tab.StatusValidity.StartsWith("Invalid")
            ? System.Windows.Media.Brushes.Red
            : System.Windows.Media.Brushes.Green;
        StatusNodes.Text = tab.StatusNodes;
    }

    private void UpdateWindowTitle(EditorTabViewModel? tab)
    {
        Title = tab is null ? "Hay Viewer" : $"{tab.Title} — Hay Viewer";
    }

    private void RebuildRecentFilesMenu()
    {
        RecentFilesMenu.Items.Clear();
        foreach (string path in _vm.RecentFiles)
        {
            var item = new MenuItem { Header = path };
            item.Click += (_, _) => OpenFile(path);
            RecentFilesMenu.Items.Add(item);
        }
        if (RecentFilesMenu.Items.Count == 0)
        {
            RecentFilesMenu.Items.Add(new MenuItem { Header = "(empty)", IsEnabled = false });
        }
    }

    private void ToggleTheme()
    {
        _vm.Settings.Theme = _vm.Settings.Theme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        ApplyTheme();
    }

    private void SetIndent(string? param)
    {
        _vm.Settings.IndentStyle = param switch
        {
            "4" => IndentStyle.FourSpaces,
            "tab" => IndentStyle.Tab,
            _ => IndentStyle.TwoSpaces
        };
        IndentCombo.SelectedIndex = _vm.Settings.IndentStyle switch
        {
            IndentStyle.FourSpaces => 1,
            IndentStyle.Tab => 2,
            _ => 0
        };
    }

    private void OpenNewWindow()
    {
        var win = new MainWindow();
        win.Show();
    }

    private void ShowAbout()
    {
        var dlg = new AboutDialog { Owner = this };
        dlg.ShowDialog();
    }

    // ─── UI event handlers ────────────────────────────────────────────────────

    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm.SelectedTab is { } tab)
        {
            UpdateWindowTitle(tab);
            Dispatcher.InvokeAsync(() => UpdateStatusBar(tab),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void IndentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            _vm.Settings.IndentStyle = IndentCombo.SelectedIndex switch
            {
                1 => IndentStyle.FourSpaces,
                2 => IndentStyle.Tab,
                _ => IndentStyle.TwoSpaces
            };
        }
    }

    private void WordWrapMenu_Click(object sender, RoutedEventArgs e)
    {
        bool wrap = WordWrapMenu.IsChecked;
        _vm.Settings.WordWrap = wrap;
        foreach (var editor in GetAllEditorViews())
            editor.IsWordWrap = wrap;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            OpenFile(files[0]);
    }
}
