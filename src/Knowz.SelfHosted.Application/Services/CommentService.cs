using Knowz.Core.Entities;
using Knowz.Core.Interfaces;
using Knowz.SelfHosted.Application.DTOs;
using Knowz.SelfHosted.Infrastructure.Data;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Knowz.SelfHosted.Application.Services;

public class CommentService
{
    private readonly SelfHostedDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<CommentService> _logger;
    private readonly IEnrichmentOutboxWriter? _enrichmentWriter;

    public CommentService(
        SelfHostedDbContext db,
        ITenantProvider tenantProvider,
        ILogger<CommentService> logger,
        IEnrichmentOutboxWriter? enrichmentWriter = null)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _logger = logger;
        _enrichmentWriter = enrichmentWriter;
    }

    public async Task<CommentResponse?> AddCommentAsync(
        Guid knowledgeId, string body, string authorName,
        Guid? parentCommentId, string? sentiment, CancellationToken ct)
    {
        var knowledge = await _db.KnowledgeItems
            .FirstOrDefaultAsync(k => k.Id == knowledgeId, ct);
        if (knowledge == null)
            return null;

        if (parentCommentId.HasValue)
        {
            var parent = await _db.Comments
                .FirstOrDefaultAsync(c => c.Id == parentCommentId.Value && c.KnowledgeId == knowledgeId, ct);
            if (parent == null)
                return null;
        }

        var tenantId = _tenantProvider.TenantId;
        var comment = new KnowledgeComment
        {
            TenantId = tenantId,
            KnowledgeId = knowledgeId,
            ParentCommentId = parentCommentId,
            AuthorName = authorName,
            Body = body,
            Sentiment = sentiment
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync(ct);

        await TriggerEnrichmentAsync(knowledgeId, tenantId, ct);

        return MapToResponse(comment, new List<CommentResponse>(), 0);
    }

    public async Task<List<CommentResponse>> ListCommentsAsync(
        Guid knowledgeId, CancellationToken ct)
    {
        var comments = await _db.Comments
            .Where(c => c.KnowledgeId == knowledgeId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        var commentIds = comments.Select(c => c.Id).ToList();
        var attachmentCounts = await _db.FileAttachments
            .Where(fa => fa.CommentId != null && commentIds.Contains(fa.CommentId.Value))
            .GroupBy(fa => fa.CommentId!.Value)
            .Select(g => new { CommentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CommentId, x => x.Count, ct);

        var topLevel = comments.Where(c => c.ParentCommentId == null).ToList();
        var byParent = comments
            .Where(c => c.ParentCommentId != null)
            .GroupBy(c => c.ParentCommentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.CreatedAt).ToList());

        return topLevel.Select(c =>
        {
            var replies = byParent.TryGetValue(c.Id, out var children)
                ? children.Select(r => MapToResponse(r, null, attachmentCounts.GetValueOrDefault(r.Id))).ToList()
                : new List<CommentResponse>();
            return MapToResponse(c, replies, attachmentCounts.GetValueOrDefault(c.Id));
        }).ToList();
    }

    public async Task<CommentResponse?> GetCommentAsync(Guid commentId, CancellationToken ct)
    {
        var comment = await _db.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId, ct);
        if (comment == null)
            return null;

        var attachmentCount = await _db.FileAttachments
            .CountAsync(fa => fa.CommentId == commentId, ct);

        return MapToResponse(comment, null, attachmentCount);
    }

    public async Task<CommentResponse?> UpdateCommentAsync(
        Guid commentId, string? body, string? sentiment, CancellationToken ct)
    {
        var comment = await _db.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId, ct);
        if (comment == null)
            return null;

        bool bodyChanged = false;
        if (body != null)
        {
            comment.Body = body;
            bodyChanged = true;
        }
        if (sentiment != null)
        {
            comment.Sentiment = sentiment;
        }

        comment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (bodyChanged)
        {
            await TriggerEnrichmentAsync(comment.KnowledgeId, comment.TenantId, ct);
        }

        var attachmentCount = await _db.FileAttachments
            .CountAsync(fa => fa.CommentId == commentId, ct);

        return MapToResponse(comment, null, attachmentCount);
    }

    public async Task<bool> DeleteCommentAsync(Guid commentId, CancellationToken ct)
    {
        var comment = await _db.Comments
            .FirstOrDefaultAsync(c => c.Id == commentId, ct);
        if (comment == null)
            return false;

        // Soft-delete the comment
        comment.IsDeleted = true;
        comment.UpdatedAt = DateTime.UtcNow;

        // Cascade soft-delete to child replies
        var childReplies = await _db.Comments
            .Where(c => c.ParentCommentId == commentId)
            .ToListAsync(ct);
        foreach (var reply in childReplies)
        {
            reply.IsDeleted = true;
            reply.UpdatedAt = DateTime.UtcNow;
        }

        // Hard-delete FileAttachment junction rows for this comment and its replies
        var commentIds = childReplies.Select(r => r.Id).Append(commentId).ToList();
        var attachments = await _db.FileAttachments
            .Where(fa => fa.CommentId != null && commentIds.Contains(fa.CommentId.Value))
            .ToListAsync(ct);
        if (attachments.Count > 0)
        {
            _db.FileAttachments.RemoveRange(attachments);
        }

        await _db.SaveChangesAsync(ct);

        await TriggerEnrichmentAsync(comment.KnowledgeId, comment.TenantId, ct);

        return true;
    }

    private async Task TriggerEnrichmentAsync(Guid knowledgeId, Guid tenantId, CancellationToken ct)
    {
        try
        {
            if (_enrichmentWriter != null)
            {
                await _enrichmentWriter.EnqueueAsync(knowledgeId, tenantId, ct);
                _logger.LogDebug("Enqueued knowledge {KnowledgeId} for re-enrichment after comment change", knowledgeId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue knowledge {KnowledgeId} for re-enrichment", knowledgeId);
        }
    }

    private static CommentResponse MapToResponse(
        KnowledgeComment comment, List<CommentResponse>? replies, int attachmentCount)
    {
        return new CommentResponse(
            comment.Id,
            comment.KnowledgeId,
            comment.ParentCommentId,
            comment.AuthorName,
            comment.Body,
            comment.IsAnswer,
            comment.Sentiment,
            comment.CreatedAt,
            comment.UpdatedAt,
            replies,
            attachmentCount);
    }
}
