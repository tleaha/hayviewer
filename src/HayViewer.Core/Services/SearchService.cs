using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HayViewer.Core.Models;

namespace HayViewer.Core.Services;

public class SearchService
{
    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Searches the JSON text for matches respecting scope (keys / values / both),
    /// case sensitivity, and optional regex.
    /// Returns character-level offsets within the original string.
    /// </summary>
    public List<SearchMatch> Search(string json, string query, SearchScope scope,
        bool caseSensitive, bool useRegex)
    {
        var matches = new List<SearchMatch>();
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(query)) return matches;

        // Build a list of (charOffset, length, isKey) for every key/value token in the document.
        var tokens = TokenizeJson(json);

        Regex? rx = null;
        if (useRegex)
        {
            try { rx = new Regex(query, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase); }
            catch { return matches; }
        }

        StringComparison sc = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        foreach (var (offset, len, isKey, rawText) in tokens)
        {
            if (scope == SearchScope.Keys && !isKey) continue;
            if (scope == SearchScope.Values && isKey) continue;

            // rawText is the content of the token (string without surrounding quotes;
            // numbers/booleans/null as literal text). We search within the raw text value
            // AND also inside the quoted representation in the source.
            string searchIn = rawText;

            if (useRegex && rx is not null)
            {
                foreach (Match m in rx.Matches(searchIn))
                {
                    // +1 to skip the opening quote for string tokens (the rawText excludes quotes
                    // but the source includes them).
                    int srcOffset = offset + (IsStringToken(json, offset) ? 1 : 0) + m.Index;
                    (int line, int col) = OffsetToLineCol(json, srcOffset);
                    matches.Add(new SearchMatch
                    {
                        Offset = srcOffset, Length = m.Length,
                        Line = line, Column = col,
                        IsKey = isKey, MatchText = m.Value
                    });
                }
            }
            else
            {
                int start = 0;
                while (true)
                {
                    int idx = searchIn.IndexOf(query, start, sc);
                    if (idx < 0) break;
                    int srcOffset = offset + (IsStringToken(json, offset) ? 1 : 0) + idx;
                    (int line, int col) = OffsetToLineCol(json, srcOffset);
                    matches.Add(new SearchMatch
                    {
                        Offset = srcOffset, Length = query.Length,
                        Line = line, Column = col,
                        IsKey = isKey, MatchText = searchIn.Substring(idx, query.Length)
                    });
                    start = idx + 1;
                }
            }
        }

        return matches;
    }

    // Tokenizes the JSON and returns (charOffset, length, isKey, rawValue) for each leaf token.
    private static List<(int offset, int length, bool isKey, string rawText)> TokenizeJson(string json)
    {
        var result = new List<(int, int, bool, string)>();
        try
        {
            // Map from byte offset to char offset.
            int[] byteToChar = BuildByteToCharTable(json);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            var reader = new Utf8JsonReader(bytes, ReaderOptions);
            bool nextIsKey = false;
            var containerStack = new Stack<bool>(); // true = object, false = array

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        containerStack.Push(true);
                        nextIsKey = true;
                        break;

                    case JsonTokenType.StartArray:
                        containerStack.Push(false);
                        nextIsKey = false;
                        break;

                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        containerStack.TryPop(out _);
                        nextIsKey = containerStack.Count > 0 && containerStack.Peek();
                        break;

                    case JsonTokenType.PropertyName:
                    {
                        int byteOff = (int)reader.TokenStartIndex;
                        int charOff = byteOff < byteToChar.Length ? byteToChar[byteOff] : byteOff;
                        // +1/-1 to skip the surrounding quotes in the source
                        int rawLen = reader.HasValueSequence
                            ? (int)reader.ValueSequence.Length
                            : reader.ValueSpan.Length;
                        result.Add((charOff, rawLen + 2, true, reader.GetString() ?? ""));
                        nextIsKey = false;
                        break;
                    }

                    case JsonTokenType.String:
                    {
                        int byteOff = (int)reader.TokenStartIndex;
                        int charOff = byteOff < byteToChar.Length ? byteToChar[byteOff] : byteOff;
                        int rawLen = reader.HasValueSequence
                            ? (int)reader.ValueSequence.Length
                            : reader.ValueSpan.Length;
                        result.Add((charOff, rawLen + 2, false, reader.GetString() ?? ""));
                        if (containerStack.Count > 0 && containerStack.Peek()) nextIsKey = true;
                        break;
                    }

                    case JsonTokenType.Number:
                    {
                        int byteOff = (int)reader.TokenStartIndex;
                        int charOff = byteOff < byteToChar.Length ? byteToChar[byteOff] : byteOff;
                        int rawLen = reader.HasValueSequence
                            ? (int)reader.ValueSequence.Length
                            : reader.ValueSpan.Length;
                        string numText = Encoding.UTF8.GetString(bytes, byteOff, rawLen);
                        result.Add((charOff, rawLen, false, numText));
                        if (containerStack.Count > 0 && containerStack.Peek()) nextIsKey = true;
                        break;
                    }

                    case JsonTokenType.True:
                    case JsonTokenType.False:
                    {
                        int byteOff = (int)reader.TokenStartIndex;
                        int charOff = byteOff < byteToChar.Length ? byteToChar[byteOff] : byteOff;
                        string boolText = reader.TokenType == JsonTokenType.True ? "true" : "false";
                        result.Add((charOff, boolText.Length, false, boolText));
                        if (containerStack.Count > 0 && containerStack.Peek()) nextIsKey = true;
                        break;
                    }

                    case JsonTokenType.Null:
                    {
                        int byteOff = (int)reader.TokenStartIndex;
                        int charOff = byteOff < byteToChar.Length ? byteToChar[byteOff] : byteOff;
                        result.Add((charOff, 4, false, "null"));
                        if (containerStack.Count > 0 && containerStack.Peek()) nextIsKey = true;
                        break;
                    }

                    case JsonTokenType.Comment:
                        break;
                }
            }
        }
        catch { }
        return result;
    }

    // Builds a table mapping byte index to char index for the given string.
    private static int[] BuildByteToCharTable(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        var table = new int[bytes.Length + 1];
        int charIdx = 0;
        int byteIdx = 0;
        while (charIdx < text.Length)
        {
            table[byteIdx] = charIdx;
            int byteLen = Encoding.UTF8.GetByteCount(text, charIdx, 1);
            // Fill multi-byte slots with the same char index
            for (int j = 1; j < byteLen && byteIdx + j < table.Length; j++)
                table[byteIdx + j] = charIdx;
            byteIdx += byteLen;
            charIdx++;
        }
        if (byteIdx < table.Length) table[byteIdx] = charIdx;
        return table;
    }

    private static bool IsStringToken(string json, int charOffset) =>
        charOffset < json.Length && json[charOffset] == '"';

    private static (int line, int col) OffsetToLineCol(string text, int offset)
    {
        int line = 1, col = 1;
        for (int i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] == '\n') { line++; col = 1; }
            else col++;
        }
        return (line, col);
    }
}
