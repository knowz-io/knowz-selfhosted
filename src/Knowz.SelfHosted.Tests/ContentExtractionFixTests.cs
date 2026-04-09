using Knowz.SelfHosted.Infrastructure.Services;

namespace Knowz.SelfHosted.Tests;

/// <summary>
/// Tests for NodeID 2: ContentExtractionFix.
/// VERIFY: DOCX/PDF extractors allow 2MB content.
/// </summary>
public class ContentExtractionFixTests
{
    // ===== DocxContentExtractor MaxExtractionChars =====

    [Fact]
    public void DocxExtractor_MaxExtractionChars_Is2MB()
    {
        // 2,097,152 = 2 * 1024 * 1024 = 2MB in chars
        Assert.Equal(2_097_152, DocxContentExtractor.MaxExtractionChars);
    }

    // ===== PdfContentExtractor MaxExtractionChars =====

    [Fact]
    public void PdfExtractor_MaxExtractionChars_Is2MB()
    {
        Assert.Equal(2_097_152, PdfContentExtractor.MaxExtractionChars);
    }

    // ===== TextEnrichmentService MaxContentChars =====

    [Fact]
    public void TextEnrichmentService_MaxContentChars_Is50000()
    {
        Assert.Equal(50_000, TextEnrichmentService.MaxContentChars);
    }

    [Fact]
    public void TruncateContent_49KContent_NotTruncated()
    {
        var content = new string('a', 49_000);
        var result = TextEnrichmentService.TruncateContent(content);
        Assert.Equal(49_000, result.Length);
    }

    [Fact]
    public void TruncateContent_51KContent_TruncatedTo50K()
    {
        var content = new string('a', 51_000);
        var result = TextEnrichmentService.TruncateContent(content);
        Assert.Equal(50_000, result.Length);
    }
}
