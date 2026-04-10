namespace Knowz.SelfHosted.Application.DTOs;

/// <summary>
/// Result of file upload operation.
/// </summary>
public record FileUploadResult(
    Guid FileRecordId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string BlobUri,
    bool Success);

/// <summary>
/// FileRecord metadata (no binary content).
/// </summary>
public record FileMetadataDto(
    Guid Id,
    string FileName,
    string? ContentType,
    long SizeBytes,
    string? BlobUri,
    string? TranscriptionText,
    string? ExtractedText,
    string? VisionDescription,
    bool BlobMigrationPending,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? KnowledgeId = null,
    string? KnowledgeTitle = null,
    Guid? VaultId = null,
    string? VaultName = null);

/// <summary>
/// FileAttachment junction record (links FileRecord to Knowledge/Comment).
/// </summary>
public record FileAttachmentDto(
    Guid Id,
    Guid FileRecordId,
    Guid? KnowledgeId,
    Guid? CommentId,
    DateTime CreatedAt);

/// <summary>
/// Paginated list of FileRecords.
/// </summary>
public record FileListResponse(
    List<FileMetadataDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
