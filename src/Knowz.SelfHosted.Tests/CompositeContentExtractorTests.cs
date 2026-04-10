using Knowz.Core.Entities;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Knowz.SelfHosted.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Knowz.SelfHosted.Tests;

public class CompositeContentExtractorTests
{
    private readonly TextFileContentExtractor _textExtractor;
    private readonly PdfContentExtractor _pdfExtractor;
    private readonly DocxContentExtractor _docxExtractor;
    private readonly CompositeContentExtractor _composite;

    public CompositeContentExtractorTests()
    {
        _textExtractor = new TextFileContentExtractor(
            Substitute.For<ILogger<TextFileContentExtractor>>());
        _pdfExtractor = new PdfContentExtractor(
            Substitute.For<ILogger<PdfContentExtractor>>());
        _docxExtractor = new DocxContentExtractor(
            Substitute.For<ILogger<DocxContentExtractor>>());

        _composite = new CompositeContentExtractor(new IFileContentExtractor[]
        {
            _textExtractor,
            _pdfExtractor,
            _docxExtractor
        });
    }

    private static FileRecord MakeRecord(string contentType, string fileName = "test")
        => new() { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), FileName = fileName, ContentType = contentType };

    // =============================================
    // CanExtract — VERIFY_COMP_01, VERIFY_COMP_02, VERIFY_COMP_03
    // =============================================

    [Fact]
    public void CanExtract_ReturnsTrue_ForPdf()
    {
        // VERIFY_COMP_01
        Assert.True(_composite.CanExtract("application/pdf"));
    }

    [Fact]
    public void CanExtract_ReturnsTrue_ForTextPlain()
    {
        // VERIFY_COMP_02
        Assert.True(_composite.CanExtract("text/plain"));
    }

    [Fact]
    public void CanExtract_ReturnsTrue_ForDocx()
    {
        Assert.True(_composite.CanExtract(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
    }

    [Fact]
    public void CanExtract_ReturnsFalse_ForImagePng()
    {
        // VERIFY_COMP_03
        Assert.False(_composite.CanExtract("image/png"));
    }

    [Fact]
    public void CanExtract_ReturnsFalse_ForNull()
    {
        Assert.False(_composite.CanExtract(null));
    }

    [Fact]
    public void CanExtract_ReturnsFalse_ForAudioMp3()
    {
        Assert.False(_composite.CanExtract("audio/mp3"));
    }

    // =============================================
    // ExtractAsync — Delegation — VERIFY_COMP_04
    // =============================================

    [Fact]
    public async Task ExtractAsync_TextPlain_DelegatesToTextExtractor()
    {
        var record = MakeRecord("text/plain", "test.txt");
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("Hello text"));

        var result = await _composite.ExtractAsync(record, stream);

        Assert.True(result.Success);
        Assert.Equal("Hello text", result.ExtractedText);
    }

    [Fact]
    public async Task ExtractAsync_Json_DelegatesToTextExtractor()
    {
        var record = MakeRecord("application/json", "data.json");
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"key\":\"value\"}"));

        var result = await _composite.ExtractAsync(record, stream);

        Assert.True(result.Success);
        Assert.Equal("{\"key\":\"value\"}", result.ExtractedText);
    }

    // =============================================
    // ExtractAsync — Unknown type — VERIFY_COMP_05
    // =============================================

    [Fact]
    public async Task ExtractAsync_UnknownType_ReturnsNoExtractorAvailable()
    {
        // VERIFY_COMP_05
        var record = MakeRecord("image/png", "photo.png");
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await _composite.ExtractAsync(record, stream);

        Assert.False(result.Success);
        Assert.Equal("No extractor available for this content type", result.ErrorMessage);
    }

    // =============================================
    // Empty extractors list
    // =============================================

    [Fact]
    public void CanExtract_WithNoExtractors_ReturnsFalse()
    {
        var empty = new CompositeContentExtractor(Array.Empty<IFileContentExtractor>());
        Assert.False(empty.CanExtract("text/plain"));
    }

    [Fact]
    public async Task ExtractAsync_WithNoExtractors_ReturnsNoExtractorAvailable()
    {
        var empty = new CompositeContentExtractor(Array.Empty<IFileContentExtractor>());
        var record = MakeRecord("text/plain");
        using var stream = new MemoryStream(new byte[] { 1 });

        var result = await empty.ExtractAsync(record, stream);

        Assert.False(result.Success);
        Assert.Equal("No extractor available for this content type", result.ErrorMessage);
    }
}
