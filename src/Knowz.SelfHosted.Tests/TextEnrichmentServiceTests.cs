using Knowz.SelfHosted.Infrastructure.Interfaces;
using Knowz.SelfHosted.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Knowz.SelfHosted.Tests;

public class TextEnrichmentServiceTests
{
    // --- NoOpTextEnrichmentService tests ---

    [Fact]
    public async Task NoOp_GenerateTitleAsync_ReturnsNull()
    {
        var logger = Substitute.For<ILogger<NoOpTextEnrichmentService>>();
        ITextEnrichmentService svc = new NoOpTextEnrichmentService(logger);

        var result = await svc.GenerateTitleAsync("some content");

        Assert.Null(result);
    }

    [Fact]
    public async Task NoOp_SummarizeAsync_ReturnsNull()
    {
        var logger = Substitute.For<ILogger<NoOpTextEnrichmentService>>();
        ITextEnrichmentService svc = new NoOpTextEnrichmentService(logger);

        var result = await svc.SummarizeAsync("some content");

        Assert.Null(result);
    }

    [Fact]
    public async Task NoOp_ExtractTagsAsync_ReturnsEmptyList()
    {
        var logger = Substitute.For<ILogger<NoOpTextEnrichmentService>>();
        ITextEnrichmentService svc = new NoOpTextEnrichmentService(logger);

        var result = await svc.ExtractTagsAsync("title", "some content");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task NoOp_ExtractTagsAsync_NeverReturnsNull()
    {
        var logger = Substitute.For<ILogger<NoOpTextEnrichmentService>>();
        ITextEnrichmentService svc = new NoOpTextEnrichmentService(logger);

        var result = await svc.ExtractTagsAsync("", "");

        Assert.NotNull(result);
    }

    // --- TextEnrichmentService static helper tests ---

    [Fact]
    public void TruncateContent_ShortContent_ReturnsSame()
    {
        var content = "Short text";
        var result = TextEnrichmentService.TruncateContent(content);
        Assert.Equal(content, result);
    }

    [Fact]
    public void TruncateContent_LongContent_Truncates()
    {
        var content = new string('x', 15_000);
        var result = TextEnrichmentService.TruncateContent(content);
        Assert.Equal(TextEnrichmentService.MaxContentChars, result.Length);
    }

    [Fact]
    public void TruncateContent_ExactlyMaxLength_ReturnsSame()
    {
        var content = new string('a', TextEnrichmentService.MaxContentChars);
        var result = TextEnrichmentService.TruncateContent(content);
        Assert.Equal(content, result);
    }

    [Fact]
    public void TruncateContent_EmptyString_ReturnsEmpty()
    {
        var result = TextEnrichmentService.TruncateContent("");
        Assert.Equal("", result);
    }

    [Fact]
    public void ParseTagsJson_ValidJsonArray_ReturnsTags()
    {
        var json = "[\"machine-learning\", \"python\", \"data-analysis\"]";
        var result = TextEnrichmentService.ParseTagsJson(json, 5);
        Assert.Equal(3, result.Count);
        Assert.Equal("machine-learning", result[0]);
        Assert.Equal("python", result[1]);
        Assert.Equal("data-analysis", result[2]);
    }

    [Fact]
    public void ParseTagsJson_MarkdownCodeBlock_ExtractsJson()
    {
        var response = "```json\n[\"tag1\", \"tag2\"]\n```";
        var result = TextEnrichmentService.ParseTagsJson(response, 5);
        Assert.Equal(2, result.Count);
        Assert.Equal("tag1", result[0]);
        Assert.Equal("tag2", result[1]);
    }

    [Fact]
    public void ParseTagsJson_MarkdownCodeBlockNoLang_ExtractsJson()
    {
        var response = "```\n[\"tag1\"]\n```";
        var result = TextEnrichmentService.ParseTagsJson(response, 5);
        Assert.Single(result);
        Assert.Equal("tag1", result[0]);
    }

    [Fact]
    public void ParseTagsJson_InvalidJson_ReturnsEmptyList()
    {
        var response = "not valid json";
        var result = TextEnrichmentService.ParseTagsJson(response, 5);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseTagsJson_RespectsMaxTags()
    {
        var json = "[\"a\", \"b\", \"c\", \"d\", \"e\", \"f\"]";
        var result = TextEnrichmentService.ParseTagsJson(json, 3);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void ParseTagsJson_FiltersEmptyStrings()
    {
        var json = "[\"valid\", \"\", \"  \", \"also-valid\"]";
        var result = TextEnrichmentService.ParseTagsJson(json, 5);
        Assert.Equal(2, result.Count);
        Assert.Equal("valid", result[0]);
        Assert.Equal("also-valid", result[1]);
    }

    [Fact]
    public void ParseTagsJson_EmptyArray_ReturnsEmptyList()
    {
        var json = "[]";
        var result = TextEnrichmentService.ParseTagsJson(json, 5);
        Assert.Empty(result);
    }
}
