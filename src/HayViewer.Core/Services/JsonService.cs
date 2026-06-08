using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using HayViewer.Core.Models;

namespace HayViewer.Core.Services;

public record JsonOperationResult(
    string Text,
    bool IsSuccess,
    string? Error = null,
    long? ErrorLine = null,
    long? ErrorColumn = null);

public class JsonService
{
    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // JsonWriter in .NET 8 always uses 2-space indent when Indented=true.
    // We post-process to convert to the requested style.
    public JsonOperationResult Format(string json, IndentStyle indent = IndentStyle.TwoSpaces)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, ParseOptions);
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions
            {
                Indented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            doc.WriteTo(writer);
            writer.Flush();
            string formatted = Encoding.UTF8.GetString(ms.ToArray());

            // The writer uses 2-space indent; convert if needed.
            if (indent != IndentStyle.TwoSpaces)
            {
                string newIndent = indent == IndentStyle.Tab ? "\t" : "    ";
                formatted = ConvertIndent(formatted, newIndent);
            }
            return new JsonOperationResult(formatted, true);
        }
        catch (JsonException ex)
        {
            return new JsonOperationResult(json, false, ex.Message,
                ex.LineNumber.HasValue ? ex.LineNumber + 1 : null,
                ex.BytePositionInLine.HasValue ? ex.BytePositionInLine + 1 : null);
        }
    }

    public JsonOperationResult Minify(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, ParseOptions);
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions
            {
                Indented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            doc.WriteTo(writer);
            writer.Flush();
            return new JsonOperationResult(Encoding.UTF8.GetString(ms.ToArray()), true);
        }
        catch (JsonException ex)
        {
            return new JsonOperationResult(json, false, ex.Message,
                ex.LineNumber.HasValue ? ex.LineNumber + 1 : null,
                ex.BytePositionInLine.HasValue ? ex.BytePositionInLine + 1 : null);
        }
    }

    public JsonOperationResult Validate(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json, ParseOptions);
            return new JsonOperationResult(json, true);
        }
        catch (JsonException ex)
        {
            return new JsonOperationResult(json, false, ex.Message,
                ex.LineNumber.HasValue ? ex.LineNumber + 1 : null,
                ex.BytePositionInLine.HasValue ? ex.BytePositionInLine + 1 : null);
        }
    }

    public int CountNodes(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, ParseOptions);
            return CountElement(doc.RootElement);
        }
        catch { return 0; }
    }

    private static int CountElement(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Object => el.EnumerateObject().Sum(p => 1 + CountElement(p.Value)),
        JsonValueKind.Array => el.EnumerateArray().Sum(CountElement),
        _ => 1
    };

    // The JsonWriter uses 2-space indent. Replace each level with the desired indent string.
    private static string ConvertIndent(string json, string newIndent)
    {
        var sb = new StringBuilder(json.Length);
        ReadOnlySpan<char> text = json.AsSpan();
        int i = 0;
        while (i <= text.Length)
        {
            // Find end of line
            int start = i;
            while (i < text.Length && text[i] != '\n') i++;
            ReadOnlySpan<char> line = text[start..i];

            // Count leading 2-space groups
            int spaces = 0;
            while (spaces + 1 < line.Length && line[spaces] == ' ' && line[spaces + 1] == ' ')
                spaces += 2;
            int level = spaces / 2;

            for (int k = 0; k < level; k++) sb.Append(newIndent);
            sb.Append(line[spaces..]);
            if (i < text.Length) { sb.Append('\n'); i++; }
            else break;
        }
        return sb.ToString();
    }
}
