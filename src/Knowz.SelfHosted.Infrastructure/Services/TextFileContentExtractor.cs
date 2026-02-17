using Knowz.Core.Entities;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace Knowz.SelfHosted.Infrastructure.Services;

public class TextFileContentExtractor : IFileContentExtractor
{
    private const int MaxExtractionBytes = 1_048_576; // 1MB

    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain", "text/markdown", "text/csv", "text/html", "text/xml",
        "application/json", "application/xml"
    };

    private readonly ILogger<TextFileContentExtractor> _logger;

    public TextFileContentExtractor(ILogger<TextFileContentExtractor> logger)
    {
        _logger = logger;
    }

    public bool CanExtract(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;

        return SupportedTypes.Contains(contentType)
            || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<FileExtractionResult> ExtractAsync(
        FileRecord fileRecord, Stream fileStream, CancellationToken ct = default)
    {
        if (!CanExtract(fileRecord.ContentType))
            return new FileExtractionResult(false, ErrorMessage: "Unsupported content type");

        try
        {
            using var reader = new StreamReader(
                fileStream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
                leaveOpen: true);

            var buffer = new char[MaxExtractionBytes / 2]; // UTF-16 chars
            var charsRead = await reader.ReadBlockAsync(buffer, 0, buffer.Length);

            if (charsRead == 0)
                return new FileExtractionResult(false, ErrorMessage: "File is empty");

            var text = new string(buffer, 0, charsRead).Trim();

            if (charsRead == buffer.Length)
            {
                _logger.LogWarning(
                    "File {FileRecordId} ({FileName}) exceeded 1MB extraction limit, content truncated",
                    fileRecord.Id, fileRecord.FileName);
            }

            return new FileExtractionResult(true, ExtractedText: text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Text extraction failed for FileRecord {Id}", fileRecord.Id);
            return new FileExtractionResult(false, ErrorMessage: ex.Message);
        }
    }
}
