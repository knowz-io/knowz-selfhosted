using Azure;
using Azure.AI.OpenAI;
using Knowz.Core.Entities;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Knowz.SelfHosted.Infrastructure.Services;

public class ImageContentExtractor : IFileContentExtractor
{
    private const int MaxOutputTokens = 4096;

    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif", "image/webp", "image/bmp", "image/tiff"
    };

    private const string SystemPrompt =
        "You are a document analyzer. Describe this image in detail, extract ALL visible text (OCR), " +
        "describe diagrams, charts, or visual elements. Be comprehensive.";

    private readonly string? _endpoint;
    private readonly string? _apiKey;
    private readonly string? _deploymentName;
    private readonly ILogger<ImageContentExtractor> _logger;

    public ImageContentExtractor(
        IConfiguration configuration,
        ILogger<ImageContentExtractor> logger)
    {
        _endpoint = configuration["AzureOpenAI:Endpoint"];
        _apiKey = configuration["AzureOpenAI:ApiKey"];
        _deploymentName = configuration["AzureOpenAI:DeploymentName"];
        _logger = logger;
    }

    public bool CanExtract(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;

        return SupportedTypes.Contains(contentType);
    }

    public async Task<FileExtractionResult> ExtractAsync(
        FileRecord fileRecord, Stream fileStream, CancellationToken ct = default)
    {
        if (!CanExtract(fileRecord.ContentType))
            return new FileExtractionResult(false, ErrorMessage: "Unsupported content type");

        // Check if OpenAI is configured
        if (string.IsNullOrWhiteSpace(_endpoint) ||
            string.IsNullOrWhiteSpace(_apiKey) ||
            string.IsNullOrWhiteSpace(_deploymentName))
        {
            _logger.LogWarning(
                "OpenAI is not configured — image extraction for {FileRecordId} ({FileName}) skipped. " +
                "Set AzureOpenAI:Endpoint, AzureOpenAI:ApiKey, and AzureOpenAI:DeploymentName to enable image extraction.",
                fileRecord.Id, fileRecord.FileName);
            return new FileExtractionResult(false,
                ErrorMessage: "OpenAI is not configured for image extraction");
        }

        try
        {
            // Read image bytes
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms, ct);
            var imageBytes = ms.ToArray();

            if (imageBytes.Length == 0)
                return new FileExtractionResult(false, ErrorMessage: "Image file is empty");

            // Create Azure OpenAI client and call vision API
            var client = new AzureOpenAIClient(
                new Uri(_endpoint),
                new AzureKeyCredential(_apiKey));

            var chatClient = client.GetChatClient(_deploymentName);

            var imageData = BinaryData.FromBytes(imageBytes);
            var mimeType = fileRecord.ContentType ?? "image/png";
            var imagePart = ChatMessageContentPart.CreateImagePart(imageData, mimeType);
            var textPart = ChatMessageContentPart.CreateTextPart("Analyze this image:");

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(SystemPrompt),
                new UserChatMessage(textPart, imagePart)
            };

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = MaxOutputTokens
            };

            var result = await chatClient.CompleteChatAsync(messages, options, ct);
            var extractedText = result.Value.Content[0].Text;

            if (string.IsNullOrWhiteSpace(extractedText))
                return new FileExtractionResult(false,
                    ErrorMessage: "OpenAI returned no text for image analysis");

            return new FileExtractionResult(true, ExtractedText: extractedText);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Image extraction failed for FileRecord {Id}", fileRecord.Id);
            return new FileExtractionResult(false, ErrorMessage: ex.Message);
        }
    }
}
