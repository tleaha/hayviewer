using System.IO;
using HayViewer.Core.Models;

namespace HayViewer.ViewModels;

public class EditorTabViewModel : ViewModelBase
{
    private string _filePath = "";
    private string _content = "";
    private bool _isDirty;
    private List<JsonNodeModel> _treeNodes = new();
    private int _caretOffset;
    private double _scrollOffset;

    public string FilePath
    {
        get => _filePath;
        set { Set(ref _filePath, value); OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(FileName)); }
    }

    public string FileName => string.IsNullOrEmpty(FilePath) ? "Untitled" : Path.GetFileName(FilePath);

    public string Title => FileName + (IsDirty ? " *" : "");

    public bool IsNew => string.IsNullOrEmpty(FilePath);

    public string Content
    {
        get => _content;
        set { Set(ref _content, value); }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set { Set(ref _isDirty, value); OnPropertyChanged(nameof(Title)); }
    }

    public List<JsonNodeModel> TreeNodes
    {
        get => _treeNodes;
        set => Set(ref _treeNodes, value);
    }

    // Editor state preserved across tab switches
    public int CaretOffset
    {
        get => _caretOffset;
        set => Set(ref _caretOffset, value);
    }

    public double ScrollOffset
    {
        get => _scrollOffset;
        set => Set(ref _scrollOffset, value);
    }

    // Status bar values updated by the editor view
    public string StatusPosition { get; set; } = "Ln 1, Col 1";
    public string StatusSize { get; set; } = "0 B";
    public string StatusValidity { get; set; } = "";
    public string StatusNodes { get; set; } = "";

    // Search state
    public string SearchQuery { get; set; } = "";
    public SearchScope SearchScope { get; set; } = SearchScope.Both;
    public bool SearchCaseSensitive { get; set; }
    public bool SearchRegex { get; set; }
    public int SearchCurrentIndex { get; set; } = -1;
    public int SearchTotalMatches { get; set; }
}
