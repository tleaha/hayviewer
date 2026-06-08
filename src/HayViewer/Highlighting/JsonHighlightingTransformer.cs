using System.Text.RegularExpressions;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Rendering;

namespace HayViewer.Highlighting;

/// <summary>
/// Applies per-token JSON syntax highlighting using regex.
/// Keys and string values are distinguished by looking for a trailing colon.
/// </summary>
public class JsonHighlightingTransformer : DocumentColorizingTransformer
{
    // String followed by optional whitespace then colon = JSON key.
    private static readonly Regex KeyPattern =
        new(@"""(?:[^""\\]|\\.)*""\s*(?=:)", RegexOptions.Compiled);
    // Any double-quoted string (values, after keys are removed from consideration).
    private static readonly Regex StringPattern =
        new(@"""(?:[^""\\]|\\.)*""", RegexOptions.Compiled);
    private static readonly Regex NumberPattern =
        new(@"-?\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b", RegexOptions.Compiled);
    private static readonly Regex BoolPattern =
        new(@"\b(?:true|false)\b", RegexOptions.Compiled);
    private static readonly Regex NullPattern =
        new(@"\bnull\b", RegexOptions.Compiled);
    private static readonly Regex PunctuationPattern =
        new(@"[{}\[\],:]", RegexOptions.Compiled);

    public SyntaxColors Colors { get; set; } = SyntaxColors.Light;

    protected override void ColorizeLine(ICSharpCode.AvalonEdit.Document.DocumentLine line)
    {
        string text = CurrentContext.Document.GetText(line);
        int start = line.Offset;

        // Track which text ranges have been colored to avoid overlapping matches.
        var colored = new List<(int s, int e)>();

        void Color(int from, int to, Brush brush)
        {
            colored.Add((from - start, to - start));
            ChangeLinePart(from, to, el =>
                el.TextRunProperties.SetForegroundBrush(brush));
        }

        bool Overlaps(int s, int e) =>
            colored.Any(r => s < r.e && e > r.s);

        // 1. Keys (must precede string value coloring)
        foreach (Match m in KeyPattern.Matches(text))
        {
            int f = start + m.Index, t = f + m.Length;
            if (!Overlaps(m.Index, m.Index + m.Length))
                Color(f, t, Colors.Key);
        }

        // 2. String values
        foreach (Match m in StringPattern.Matches(text))
        {
            if (!Overlaps(m.Index, m.Index + m.Length))
                Color(start + m.Index, start + m.Index + m.Length, Colors.StringValue);
        }

        // 3. Numbers
        foreach (Match m in NumberPattern.Matches(text))
        {
            if (!Overlaps(m.Index, m.Index + m.Length))
                Color(start + m.Index, start + m.Index + m.Length, Colors.Number);
        }

        // 4. Booleans
        foreach (Match m in BoolPattern.Matches(text))
        {
            if (!Overlaps(m.Index, m.Index + m.Length))
                Color(start + m.Index, start + m.Index + m.Length, Colors.Boolean);
        }

        // 5. Null
        foreach (Match m in NullPattern.Matches(text))
        {
            if (!Overlaps(m.Index, m.Index + m.Length))
                Color(start + m.Index, start + m.Index + m.Length, Colors.Null);
        }

        // 6. Punctuation
        foreach (Match m in PunctuationPattern.Matches(text))
        {
            if (!Overlaps(m.Index, m.Index + m.Length))
                Color(start + m.Index, start + m.Index + m.Length, Colors.Punctuation);
        }
    }
}

public class SyntaxColors
{
    public Brush Key { get; init; } = Brushes.Black;
    public Brush StringValue { get; init; } = Brushes.Black;
    public Brush Number { get; init; } = Brushes.Black;
    public Brush Boolean { get; init; } = Brushes.Black;
    public Brush Null { get; init; } = Brushes.Black;
    public Brush Punctuation { get; init; } = Brushes.Black;
    public Brush Default { get; init; } = Brushes.Black;
    public Brush Background { get; init; } = Brushes.White;

    // Light theme matching the spec palette.
    public static readonly SyntaxColors Light = new()
    {
        Key = new SolidColorBrush(Color.FromRgb(0x8B, 0x1A, 0x1A)),
        StringValue = new SolidColorBrush(Color.FromRgb(0x1A, 0x7F, 0x37)),
        Number = new SolidColorBrush(Color.FromRgb(0x05, 0x50, 0xAE)),
        Boolean = new SolidColorBrush(Color.FromRgb(0x82, 0x50, 0xDF)),
        Null = new SolidColorBrush(Color.FromRgb(0x6E, 0x77, 0x81)),
        Punctuation = new SolidColorBrush(Color.FromRgb(0x57, 0x60, 0x6A)),
        Default = new SolidColorBrush(Color.FromRgb(0x1F, 0x23, 0x28)),
        Background = Brushes.White
    };

    // Dark theme.
    public static readonly SyntaxColors Dark = new()
    {
        Key = new SolidColorBrush(Color.FromRgb(0xFF, 0x7B, 0x72)),
        StringValue = new SolidColorBrush(Color.FromRgb(0x7E, 0xE7, 0x87)),
        Number = new SolidColorBrush(Color.FromRgb(0x79, 0xC0, 0xFF)),
        Boolean = new SolidColorBrush(Color.FromRgb(0xD2, 0xA8, 0xFF)),
        Null = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
        Punctuation = new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E)),
        Default = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
        Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17))
    };
}
