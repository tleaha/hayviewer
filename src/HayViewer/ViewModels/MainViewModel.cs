using System.Collections.ObjectModel;
using System.Windows.Input;
using HayViewer.Core.Models;
using HayViewer.Core.Services;

namespace HayViewer.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService = new();
    private EditorTabViewModel? _selectedTab;
    private AppSettings _settings;

    public MainViewModel()
    {
        _settings = _settingsService.Load();
        CloseTabCommand = new RelayCommand(CloseTab);
        Tabs.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasTabs));
            OnPropertyChanged(nameof(RecentFiles));
        };
    }

    public ObservableCollection<EditorTabViewModel> Tabs { get; } = new();

    public EditorTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set => Set(ref _selectedTab, value);
    }

    public bool HasTabs => Tabs.Count > 0;

    public AppSettings Settings
    {
        get => _settings;
        private set => Set(ref _settings, value);
    }

    public List<string> RecentFiles => _settings.RecentFiles;

    // Commands wired from MainWindow code-behind so they can access view state.
    public ICommand? OpenCommand { get; set; }
    public ICommand? SaveCommand { get; set; }
    public ICommand? SaveAsCommand { get; set; }
    public ICommand? NewTabCommand { get; set; }
    public ICommand? FormatCommand { get; set; }
    public ICommand? MinifyCommand { get; set; }
    public ICommand? ValidateCommand { get; set; }
    public ICommand? FindCommand { get; set; }
    public ICommand? FindNextCommand { get; set; }
    public ICommand? FindPrevCommand { get; set; }
    public ICommand? RefreshTreeCommand { get; set; }
    public ICommand? ToggleThemeCommand { get; set; }
    public ICommand? NewWindowCommand { get; set; }
    public ICommand? ExitCommand { get; set; }
    public ICommand? AboutCommand { get; set; }
    public ICommand? SetIndentCommand { get; set; }
    public ICommand CloseTabCommand { get; }

    public EditorTabViewModel OpenNewTab(string? filePath = null, string? content = null)
    {
        var tab = new EditorTabViewModel
        {
            FilePath = filePath ?? "",
            Content = content ?? ""
        };
        Tabs.Add(tab);
        SelectedTab = tab;
        return tab;
    }

    public bool TryFindExistingTab(string filePath, out EditorTabViewModel? tab)
    {
        tab = Tabs.FirstOrDefault(t =>
            string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        return tab is not null;
    }

    private void CloseTab(object? param)
    {
        if (param is EditorTabViewModel tab)
            Tabs.Remove(tab);
    }

    public void AddRecentFile(string path)
    {
        _settingsService.AddRecentFile(_settings, path);
        OnPropertyChanged(nameof(RecentFiles));
        SaveSettings();
    }

    public void SaveSettings() => _settingsService.Save(_settings);
}
