namespace Knowz.SelfHosted.Application.DTOs;

public record CommentResponse(
    Guid Id,
    Guid KnowledgeId,
    Guid? ParentCommentId,
    string AuthorName,
    string Body,
    bool IsAnswer,
    string? Sentiment,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<CommentResponse>? Replies,
    int AttachmentCount);
