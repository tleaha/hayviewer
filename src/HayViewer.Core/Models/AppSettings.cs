namespace HayViewer.Core.Models;

public enum IndentStyle { TwoSpaces, FourSpaces, Tab }
public enum AppTheme { Light, Dark }
public enum SearchScope { Keys, Values, Both }

public class AppSettings
{
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 800;
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public bool IsMaximized { get; set; } = false;
    public IndentStyle IndentStyle { get; set; } = IndentStyle.TwoSpaces;
    public AppTheme Theme { get; set; } = AppTheme.Light;
    public List<string> RecentFiles { get; set; } = new();
    public double TreeViewWidth { get; set; } = 280;
    public bool WordWrap { get; set; } = false;
    public const int MaxRecentFiles = 10;
}
