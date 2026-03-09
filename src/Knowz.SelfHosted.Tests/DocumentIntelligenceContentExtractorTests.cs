using Knowz.Core.Entities;
using Knowz.SelfHosted.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Knowz.SelfHosted.Tests;

public class DocumentIntelligenceContentExtractorTests
{
    private static FileRecord MakeRecord(string contentType, string fileName = "test")
        => new() { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), FileName = fileName, ContentType = contentType };

    // =============================================
    // CanExtract
    // =============================================

    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("image/tiff", true)]
    [InlineData("image/bmp", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/json", false)]
    [InlineData("audio/mp3", false)]
    [InlineData("application/octet-stream", false)]
    public void CanExtract_ReturnsExpected(string contentType, bool expected)
    {
        // Create with null client — we're only testing CanExtract which doesn't use client
        var logger = Substitute.For<ILogger<DocumentIntelligenceContentExtractor>>();
        var extractor = new DocumentIntelligenceContentExtractor(null!, logger);

        Assert.Equal(expected, extractor.CanExtract(contentType));
    }

    [Fact]
    public void CanExtract_ReturnsFalse_ForNull()
    {
        var logger = Substitute.For<ILogger<DocumentIntelligenceContentExtractor>>();
        var extractor = new DocumentIntelligenceContentExtractor(null!, logger);

        Assert.False(extractor.CanExtract(null));
    }

    [Fact]
    public void CanExtract_ReturnsFalse_ForEmpty()
    {
        var logger = Substitute.For<ILogger<DocumentIntelligenceContentExtractor>>();
        var extractor = new DocumentIntelligenceContentExtractor(null!, logger);

        Assert.False(extractor.CanExtract(""));
    }

    // =============================================
    // ExtractAsync — unsupported type
    // =============================================

    [Fact]
    public async Task ExtractAsync_ReturnsFailure_ForUnsupportedType()
    {
        var logger = Substitute.For<ILogger<DocumentIntelligenceContentExtractor>>();
        var extractor = new DocumentIntelligenceContentExtractor(null!, logger);

        var record = MakeRecord("text/plain", "test.txt");
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await extractor.ExtractAsync(record, stream);

        Assert.False(result.Success);
        Assert.Equal("Unsupported content type", result.ErrorMessage);
    }
}
