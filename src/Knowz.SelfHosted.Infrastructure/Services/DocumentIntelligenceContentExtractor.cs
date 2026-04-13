using Azure;
using Azure.AI.DocumentIntelligence;
using Knowz.Core.Entities;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace Knowz.SelfHosted.Infrastructure.Services;

public class DocumentIntelligenceContentExtractor : IFileContentExtractor
{
    private const int MaxExtractionChars = 10_000_000; // 10M chars

    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/jpeg", "image/png", "image/tiff", "image/bmp"
    };

    private readonly DocumentIntelligenceClient _client;
    private readonly ILogger<DocumentIntelligenceContentExtractor> _logger;

    public DocumentIntelligenceContentExtractor(
        DocumentIntelligenceClient client,
        ILogger<DocumentIntelligenceContentExtractor> logger)
    {
        _client = client;
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

        try
        {
            // Buffer stream into BinaryData for the API
            var bytesSource = await ReadStreamAsBinaryDataAsync(fileStream, ct);

            var operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed, "prebuilt-read", bytesSource, ct);

            var analyzeResult = operation.Value;
            var textParts = new List<string>();
            var totalChars = 0;

            foreach (var page in analyzeResult.Pages)
            {
                ct.ThrowIfCancellationRequested();

                foreach (var line in page.Lines)
                {
                    var lineText = line.Content;
                    if (string.IsNullOrEmpty(lineText))
                        continue;

                    if (totalChars + lineText.Length > MaxExtractionChars)
                    {
                        var remaining = MaxExtractionChars - totalChars;
                        if (remaining > 0)
                            textParts.Add(lineText[..remaining]);

                        _logger.LogWarning(
                            "Document Intelligence extraction for {FileRecordId} ({FileName}) exceeded limit at page {Page}, content truncated",
                            fileRecord.Id, fileRecord.FileName, page.PageNumber);
                        goto done;
                    }

                    textParts.Add(lineText);
                    totalChars += lineText.Length;
                }
            }

            done:
            if (textParts.Count == 0)
                return new FileExtractionResult(false,
                    ErrorMessage: "Document Intelligence returned no extractable text");

            var text = string.Join("\n", textParts);
            return new FileExtractionResult(true, ExtractedText: text);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Document Intelligence extraction failed for FileRecord {Id}", fileRecord.Id);
            return new FileExtractionResult(false, ErrorMessage: ex.Message);
        }
    }

    private static async Task<BinaryData> ReadStreamAsBinaryDataAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return BinaryData.FromBytes(ms.ToArray());
    }
}
