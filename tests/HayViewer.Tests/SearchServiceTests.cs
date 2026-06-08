using HayViewer.Core.Models;
using HayViewer.Core.Services;

namespace HayViewer.Tests;

public class SearchServiceTests
{
    private readonly SearchService _svc = new();

    private const string SampleJson = """
    {
      "name": "Alice",
      "age": 30,
      "city": "Wonderland",
      "active": true,
      "score": null
    }
    """;

    // ─── Scope: Both ──────────────────────────────────────────────────────────

    [Fact]
    public void Search_Both_FindsKeyAndValue()
    {
        var matches = _svc.Search(SampleJson, "name", SearchScope.Both, false, false);
        Assert.True(matches.Count >= 1);
        Assert.Contains(matches, m => m.IsKey);
    }

    [Fact]
    public void Search_Both_FindsValueMatch()
    {
        var matches = _svc.Search(SampleJson, "Alice", SearchScope.Both, false, false);
        Assert.True(matches.Count >= 1);
        Assert.Contains(matches, m => !m.IsKey);
    }

    // ─── Scope: Keys ──────────────────────────────────────────────────────────

    [Fact]
    public void Search_KeysOnly_DoesNotMatchValues()
    {
        var matches = _svc.Search(SampleJson, "Alice", SearchScope.Keys, false, false);
        Assert.Empty(matches);
    }

    [Fact]
    public void Search_KeysOnly_FindsMatchingKey()
    {
        var matches = _svc.Search(SampleJson, "age", SearchScope.Keys, false, false);
        Assert.True(matches.Count >= 1);
        Assert.All(matches, m => Assert.True(m.IsKey));
    }

    // ─── Scope: Values ────────────────────────────────────────────────────────

    [Fact]
    public void Search_ValuesOnly_DoesNotMatchKeys()
    {
        var matches = _svc.Search(SampleJson, "name", SearchScope.Values, false, false);
        // "name" only appears as a key in SampleJson — no value matches
        Assert.Empty(matches);
    }

    [Fact]
    public void Search_ValuesOnly_FindsValueMatch()
    {
        var matches = _svc.Search(SampleJson, "Alice", SearchScope.Values, false, false);
        Assert.True(matches.Count >= 1);
        Assert.All(matches, m => Assert.False(m.IsKey));
    }

    // ─── Case sensitivity ─────────────────────────────────────────────────────

    [Fact]
    public void Search_CaseSensitive_NoMatchWhenCaseDiffers()
    {
        var matches = _svc.Search(SampleJson, "ALICE", SearchScope.Both, caseSensitive: true, useRegex: false);
        Assert.Empty(matches);
    }

    [Fact]
    public void Search_CaseInsensitive_MatchesRegardlessOfCase()
    {
        var matches = _svc.Search(SampleJson, "ALICE", SearchScope.Both, caseSensitive: false, useRegex: false);
        Assert.True(matches.Count >= 1);
    }

    // ─── Regex ────────────────────────────────────────────────────────────────

    [Fact]
    public void Search_Regex_MatchesPattern()
    {
        // Match any value starting with "A"
        var matches = _svc.Search(SampleJson, "^A", SearchScope.Values, false, useRegex: true);
        Assert.True(matches.Count >= 1);
        Assert.All(matches, m => Assert.StartsWith("A", m.MatchText, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Search_InvalidRegex_ReturnsEmpty()
    {
        // An invalid regex pattern should not throw; return empty list
        var matches = _svc.Search(SampleJson, "[invalid(", SearchScope.Both, false, useRegex: true);
        Assert.Empty(matches);
    }

    // ─── Match count ─────────────────────────────────────────────────────────

    [Fact]
    public void Search_ReturnsCorrectMatchCount()
    {
        string json = """{"a":"test","b":"test","c":"other"}""";
        var matches = _svc.Search(json, "test", SearchScope.Values, false, false);
        Assert.Equal(2, matches.Count);
    }

    // ─── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public void Search_EmptyJson_ReturnsEmpty()
    {
        var matches = _svc.Search("", "name", SearchScope.Both, false, false);
        Assert.Empty(matches);
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsEmpty()
    {
        var matches = _svc.Search(SampleJson, "", SearchScope.Both, false, false);
        Assert.Empty(matches);
    }

    [Fact]
    public void Search_InvalidJson_ReturnsEmpty()
    {
        var matches = _svc.Search("{bad json}", "name", SearchScope.Both, false, false);
        Assert.Empty(matches);
    }
}
