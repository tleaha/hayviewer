using System.Windows;
using System.Windows.Media;
using HayViewer.Core.Models;
using ICSharpCode.AvalonEdit.Rendering;

namespace HayViewer.Highlighting;

/// <summary>
/// Draws yellow highlight boxes behind search match ranges in the editor.
/// </summary>
public class SearchHighlightRenderer : IBackgroundRenderer
{
    private List<SearchMatch> _matches = new();
    private int _currentIndex = -1;

    public KnownLayer Layer => KnownLayer.Background;

    public void SetMatches(IEnumerable<SearchMatch> matches, int currentIndex)
    {
        _matches = matches.ToList();
        _currentIndex = currentIndex;
    }

    public void Clear()
    {
        _matches.Clear();
        _currentIndex = -1;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_matches.Count == 0) return;

        textView.EnsureVisualLines();

        var allMatchBrush = new SolidColorBrush(Color.FromArgb(80, 0xFF, 0xE0, 0x00));
        var currentBrush = new SolidColorBrush(Color.FromArgb(180, 0xFF, 0x96, 0x00));
        allMatchBrush.Freeze();
        currentBrush.Freeze();

        for (int i = 0; i < _matches.Count; i++)
        {
            var match = _matches[i];
            int endOffset = match.Offset + match.Length;
            if (endOffset > textView.Document.TextLength) continue;

            var brush = i == _currentIndex ? currentBrush : allMatchBrush;
            var segment = new SimpleSegment(match.Offset, match.Length);
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                drawingContext.DrawRectangle(brush, null, new Rect(rect.Location,
                    new Size(Math.Max(rect.Width, 2), rect.Height)));
            }
        }
    }
}

// Minimal ISegment implementation for AvalonEdit.
internal readonly struct SimpleSegment : ICSharpCode.AvalonEdit.Document.ISegment
{
    public SimpleSegment(int offset, int length) { Offset = offset; Length = length; }
    public int Offset { get; }
    public int Length { get; }
    public int EndOffset => Offset + Length;
}
