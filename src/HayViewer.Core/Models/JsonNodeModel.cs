namespace HayViewer.Core.Models;

public enum JsonNodeKind { Object, Array, String, Number, Boolean, Null }

public class JsonNodeModel
{
    public string? Key { get; init; }
    public string? ValueText { get; init; }
    public JsonNodeKind Kind { get; init; }
    public int Line { get; init; }
    public int TextOffset { get; init; }
    public List<JsonNodeModel> Children { get; } = new();
    public bool IsExpanded { get; set; } = true;

    public string Icon => Kind switch
    {
        JsonNodeKind.Object => "{}",
        JsonNodeKind.Array => "[]",
        JsonNodeKind.String => "\"\"",
        JsonNodeKind.Number => "#",
        JsonNodeKind.Boolean => "✓",
        JsonNodeKind.Null => "∅",
        _ => "?"
    };

    public string DisplayText
    {
        get
        {
            string val = Kind switch
            {
                JsonNodeKind.Object => $"({Children.Count} items)",
                JsonNodeKind.Array => $"[{Children.Count} items]",
                JsonNodeKind.String => $"\"{TruncateValue(ValueText, 80)}\"",
                JsonNodeKind.Null => "null",
                _ => ValueText ?? ""
            };
            return Key is not null ? $"\"{Key}\": {val}" : val;
        }
    }

    private static string TruncateValue(string? s, int max) =>
        s is null ? "" : s.Length <= max ? s : s[..max] + "…";
}
