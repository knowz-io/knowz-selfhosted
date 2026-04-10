using Knowz.Core.Entities;
using Knowz.SelfHosted.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Knowz.SelfHosted.Tests;

public class ImageContentExtractorTests
{
    private readonly ILogger<ImageContentExtractor> _logger;

    public ImageContentExtractorTests()
    {
        _logger = Substitute.For<ILogger<ImageContentExtractor>>();
    }

    private static FileRecord MakeRecord(string contentType, string fileName = "test.png")
        => new() { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), FileName = fileName, ContentType = contentType };

    private static IConfiguration CreateEmptyConfig()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
    }

    private static IConfiguration CreateConfigWithOpenAI()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureOpenAI:Endpoint"] = "https://test.openai.azure.com/",
                ["AzureOpenAI:ApiKey"] = "test-api-key",
                ["AzureOpenAI:DeploymentName"] = "gpt-4o"
            })
            .Build();
    }

    // =============================================
    // CanExtract
    // =============================================

    [Theory]
    [InlineData("image/png", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("image/gif", true)]
    [InlineData("image/webp", true)]
    [InlineData("image/bmp", true)]
    [InlineData("image/tiff", true)]
    [InlineData("IMAGE/PNG", true)]
    [InlineData("Image/Jpeg", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/pdf", false)]
    [InlineData("audio/mp3", false)]
    [InlineData("video/mp4", false)]
    public void Should_CanExtract_ReturnExpected(string contentType, bool expected)
    {
        var config = CreateEmptyConfig();
        var extractor = new ImageContentExtractor(config, _logger);

        Assert.Equal(expected, extractor.CanExtract(contentType));
    }

    [Fact]
    public void Should_CanExtract_ReturnFalse_WhenNull()
    {
        var config = CreateEmptyConfig();
        var extractor = new ImageContentExtractor(config, _logger);

        Assert.False(extractor.CanExtract(null));
    }

    [Fact]
    public void Should_CanExtract_ReturnFalse_WhenEmpty()
    {
        var config = CreateEmptyConfig();
        var extractor = new ImageContentExtractor(config, _logger);

        Assert.False(extractor.CanExtract(""));
    }

    // =============================================
    // ExtractAsync — OpenAI NOT configured
    // =============================================

    [Fact]
    public async Task Should_ReturnEmptyResult_WhenOpenAINotConfigured()
    {
        var config = CreateEmptyConfig();
        var extractor = new ImageContentExtractor(config, _logger);
        var record = MakeRecord("image/png");
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header

        var result = await extractor.ExtractAsync(record, stream);

        // When OpenAI is not configured, should return failure gracefully
        Assert.False(result.Success);
        Assert.Contains("not configured", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    // =============================================
    // ExtractAsync — Unsupported content type
    // =============================================

    [Fact]
    public async Task Should_ReturnFailure_WhenUnsupportedContentType()
    {
        var config = CreateEmptyConfig();
        var extractor = new ImageContentExtractor(config, _logger);
        var record = MakeRecord("text/plain", "test.txt");
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await extractor.ExtractAsync(record, stream);

        Assert.False(result.Success);
        Assert.Equal("Unsupported content type", result.ErrorMessage);
    }
}
