using HayViewer.Core.Models;
using HayViewer.Core.Services;

namespace HayViewer.Tests;

public class JsonServiceTests
{
    private readonly JsonService _svc = new();

    // ─── Format ──────────────────────────────────────────────────────────────

    [Fact]
    public void Format_ValidJson_ProducesIndentedOutput()
    {
        string input = """{"name":"Alice","age":30}""";
        var result = _svc.Format(input, IndentStyle.TwoSpaces);
        Assert.True(result.IsSuccess);
        Assert.Contains("\n", result.Text);
        Assert.Contains("  \"name\"", result.Text);
    }

    [Fact]
    public void Format_FourSpaces_UsesCorrectIndent()
    {
        string input = """{"a":1}""";
        var result = _svc.Format(input, IndentStyle.FourSpaces);
        Assert.True(result.IsSuccess);
        Assert.Contains("    \"a\"", result.Text);
    }

    [Fact]
    public void Format_Tab_UsesTabIndent()
    {
        string input = """{"a":1}""";
        var result = _svc.Format(input, IndentStyle.Tab);
        Assert.True(result.IsSuccess);
        Assert.Contains("\t\"a\"", result.Text);
    }

    [Fact]
    public void Format_InvalidJson_ReturnsError()
    {
        var result = _svc.Format("{invalid}");
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.DoesNotContain("Exception", result.Text); // should not throw or crash
    }

    [Fact]
    public void Format_InvalidJson_ProvidesLineAndColumn()
    {
        string bad = "{\n  \"key\": BADVALUE\n}";
        var result = _svc.Format(bad);
        Assert.False(result.IsSuccess);
        Assert.True(result.ErrorLine.HasValue, "Should report error line");
    }

    // ─── Minify ───────────────────────────────────────────────────────────────

    [Fact]
    public void Minify_ValidJson_RemovesWhitespace()
    {
        string input = """
        {
          "name": "Alice",
          "age": 30
        }
        """;
        var result = _svc.Minify(input);
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("\n", result.Text);
        Assert.DoesNotContain("  ", result.Text);
        Assert.Contains("\"name\":\"Alice\"", result.Text);
    }

    [Fact]
    public void Minify_InvalidJson_ReturnsError()
    {
        var result = _svc.Minify("[1,2,broken]");
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    // ─── Validate ─────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidJson_ReturnsSuccess()
    {
        var result = _svc.Validate("""{"ok":true}""");
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsErrorWithPosition()
    {
        string bad = "{\n  \"broken\": \n}";
        var result = _svc.Validate(bad);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.True(result.ErrorLine.HasValue);
    }

    [Fact]
    public void Validate_EmptyObject_Valid()
    {
        Assert.True(_svc.Validate("{}").IsSuccess);
    }

    [Fact]
    public void Validate_NestedStructure_Valid()
    {
        string json = """{"users":[{"id":1,"name":"Bob"},{"id":2,"name":"Alice"}]}""";
        Assert.True(_svc.Validate(json).IsSuccess);
    }

    // ─── CountNodes ───────────────────────────────────────────────────────────

    [Fact]
    public void CountNodes_ReturnsCorrectCount()
    {
        // {"a":1,"b":"x"} counts each property + each value = 4 nodes total
        Assert.Equal(4, _svc.CountNodes("""{"a":1,"b":"x"}"""));
    }

    [Fact]
    public void CountNodes_InvalidJson_ReturnsZero()
    {
        Assert.Equal(0, _svc.CountNodes("{bad}"));
    }
}
