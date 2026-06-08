using System.Text;
using System.Text.Json;
using HayViewer.Core.Models;

namespace HayViewer.Core.Services;

public class JsonTreeBuilder
{
    public List<JsonNodeModel> Build(string json)
    {
        var result = new List<JsonNodeModel>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            int[] lineStarts = BuildLineStartTable(json);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            // Advance to the first real token
            if (reader.Read())
            {
                var node = ParseCurrent(ref reader, null, lineStarts, bytes);
                if (node is not null) result.Add(node);
            }
        }
        catch { /* return partial tree on malformed input */ }
        return result;
    }

    // Parses the value already sitting in reader.TokenType (no extra Read call).
    private static JsonNodeModel? ParseCurrent(ref Utf8JsonReader reader, string? key,
        int[] lineStarts, byte[] bytes)
    {
        int offset = (int)reader.TokenStartIndex;
        int line = ByteOffsetToLine(lineStarts, offset);

        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
            {
                var node = new JsonNodeModel { Key = key, Kind = JsonNodeKind.Object, Line = line, TextOffset = offset };
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string propKey = reader.GetString() ?? "";
                    if (reader.Read())
                    {
                        var child = ParseCurrent(ref reader, propKey, lineStarts, bytes);
                        if (child is not null) node.Children.Add(child);
                    }
                }
                return node;
            }

            case JsonTokenType.StartArray:
            {
                var node = new JsonNodeModel { Key = key, Kind = JsonNodeKind.Array, Line = line, TextOffset = offset };
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    var child = ParseCurrent(ref reader, null, lineStarts, bytes);
                    if (child is not null) node.Children.Add(child);
                }
                return node;
            }

            case JsonTokenType.String:
                return new JsonNodeModel { Key = key, Kind = JsonNodeKind.String, ValueText = reader.GetString(), Line = line, TextOffset = offset };

            case JsonTokenType.Number:
            {
                int len = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                string numText = len > 0 ? Encoding.UTF8.GetString(bytes, offset, Math.Min(len, bytes.Length - offset)) : "0";
                return new JsonNodeModel { Key = key, Kind = JsonNodeKind.Number, ValueText = numText, Line = line, TextOffset = offset };
            }

            case JsonTokenType.True:
                return new JsonNodeModel { Key = key, Kind = JsonNodeKind.Boolean, ValueText = "true", Line = line, TextOffset = offset };

            case JsonTokenType.False:
                return new JsonNodeModel { Key = key, Kind = JsonNodeKind.Boolean, ValueText = "false", Line = line, TextOffset = offset };

            case JsonTokenType.Null:
                return new JsonNodeModel { Key = key, Kind = JsonNodeKind.Null, ValueText = "null", Line = line, TextOffset = offset };

            default:
                return null;
        }
    }

    // Returns byte offsets of the start of each line in the UTF-8 encoded text.
    private static int[] BuildLineStartTable(string text)
    {
        var offsets = new List<int> { 0 };
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        int byteOffset = 0;
        char[] charBuf = new char[1];
        byte[] byteBuf = new byte[4];
        for (int i = 0; i < text.Length; i++)
        {
            charBuf[0] = text[i];
            int written = Encoding.UTF8.GetBytes(charBuf, 0, 1, byteBuf, 0);
            byteOffset += written;
            if (text[i] == '\n') offsets.Add(byteOffset);
        }
        return offsets.ToArray();
    }

    private static int ByteOffsetToLine(int[] lineStarts, int byteOffset)
    {
        // Binary search for the last line that starts at or before byteOffset.
        int lo = 0, hi = lineStarts.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (lineStarts[mid] <= byteOffset) lo = mid;
            else hi = mid - 1;
        }
        return lo + 1; // 1-based
    }
}
