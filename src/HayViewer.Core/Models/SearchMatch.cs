namespace HayViewer.Core.Models;

public class SearchMatch
{
    public int Offset { get; init; }   // char offset in the source text
    public int Length { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public bool IsKey { get; init; }
    public string MatchText { get; init; } = "";
}
