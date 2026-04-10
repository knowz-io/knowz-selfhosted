using Knowz.Core.Entities;
using Knowz.Core.Interfaces;
using Knowz.SelfHosted.Application.DTOs;
using Knowz.SelfHosted.Application.Services;
using Knowz.SelfHosted.Infrastructure.Data;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Knowz.SelfHosted.Tests;

public class CommentServiceTests : IDisposable
{
    private readonly SelfHostedDbContext _db;
    private readonly CommentService _svc;
    private readonly IEnrichmentOutboxWriter _enrichmentWriter;
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000099");

    public CommentServiceTests()
    {
        var options = new DbContextOptionsBuilder<SelfHostedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(TenantId);

        _db = new SelfHostedDbContext(options, tenantProvider);
        _enrichmentWriter = Substitute.For<IEnrichmentOutboxWriter>();

        var logger = Substitute.For<ILogger<CommentService>>();
        _svc = new CommentService(_db, tenantProvider, logger, _enrichmentWriter);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    // --- Helpers ---

    private async Task<Knowledge> SeedKnowledge(string title = "Test Knowledge", string content = "Test content")
    {
        var item = new Knowledge
        {
            TenantId = TenantId,
            Title = title,
            Content = content
        };
        _db.KnowledgeItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    // =============================================
    // SVC_CommentService: AddCommentAsync
    // =============================================

    [Fact]
    public async Task AddComment_CreatesComment_WithCorrectFields()
    {
        var knowledge = await SeedKnowledge();

        var result = await _svc.AddCommentAsync(
            knowledge.Id, "Hello world", "Alice", null, "positive", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(knowledge.Id, result.KnowledgeId);
        Assert.Equal("Hello world", result.Body);
        Assert.Equal("Alice", result.AuthorName);
        Assert.Equal("positive", result.Sentiment);
        Assert.Null(result.ParentCommentId);
        Assert.False(result.IsAnswer);

        // Verify persisted in DB
        var saved = await _db.Comments.FindAsync(result.Id);
        Assert.NotNull(saved);
        Assert.Equal(TenantId, saved.TenantId);
    }

    [Fact]
    public async Task AddComment_ReturnsNull_WhenKnowledgeNotFound()
    {
        var result = await _svc.AddCommentAsync(
            Guid.NewGuid(), "Body", "Author", null, null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task AddComment_CreatesReply_WithValidParentCommentId()
    {
        var knowledge = await SeedKnowledge();
        var parent = await _svc.AddCommentAsync(
            knowledge.Id, "Parent comment", "Alice", null, null, CancellationToken.None);

        var reply = await _svc.AddCommentAsync(
            knowledge.Id, "Reply to parent", "Bob", parent!.Id, null, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal(parent.Id, reply.ParentCommentId);
        Assert.Equal(knowledge.Id, reply.KnowledgeId);
    }

    [Fact]
    public async Task AddComment_ReturnsNull_WhenParentCommentNotFound()
    {
        var knowledge = await SeedKnowledge();

        var result = await _svc.AddCommentAsync(
            knowledge.Id, "Body", "Author", Guid.NewGuid(), null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task AddComment_ReturnsNull_WhenParentBelongsToDifferentKnowledge()
    {
        var knowledge1 = await SeedKnowledge("K1");
        var knowledge2 = await SeedKnowledge("K2");

        var parentOnK1 = await _svc.AddCommentAsync(
            knowledge1.Id, "Parent on K1", "Alice", null, null, CancellationToken.None);

        var result = await _svc.AddCommentAsync(
            knowledge2.Id, "Reply on K2", "Bob", parentOnK1!.Id, null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task AddComment_TriggersEnrichment()
    {
        var knowledge = await SeedKnowledge();

        await _svc.AddCommentAsync(
            knowledge.Id, "Body", "Author", null, null, CancellationToken.None);

        await _enrichmentWriter.Received(1).EnqueueAsync(
            knowledge.Id, TenantId, Arg.Any<CancellationToken>());
    }

    // =============================================
    // SVC_CommentService: ListCommentsAsync
    // =============================================

    [Fact]
    public async Task ListComments_ReturnsTopLevelWithNestedReplies()
    {
        var knowledge = await SeedKnowledge();
        var parent = await _svc.AddCommentAsync(
            knowledge.Id, "Top level", "Alice", null, null, CancellationToken.None);
        await _svc.AddCommentAsync(
            knowledge.Id, "Reply 1", "Bob", parent!.Id, null, CancellationToken.None);
        await _svc.AddCommentAsync(
            knowledge.Id, "Reply 2", "Charlie", parent.Id, null, CancellationToken.None);

        var result = await _svc.ListCommentsAsync(knowledge.Id, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Top level", result[0].Body);
        Assert.NotNull(result[0].Replies);
        Assert.Equal(2, result[0].Replies!.Count);
        Assert.Equal("Reply 1", result[0].Replies![0].Body);
        Assert.Equal("Reply 2", result[0].Replies![1].Body);
    }

    [Fact]
    public async Task ListComments_ReturnsEmptyList_WhenNoComments()
    {
        var knowledge = await SeedKnowledge();

        var result = await _svc.ListCommentsAsync(knowledge.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ListComments_OrderedByCreatedAtAscending()
    {
        var knowledge = await SeedKnowledge();
        await _svc.AddCommentAsync(knowledge.Id, "First", "A", null, null, CancellationToken.None);
        await _svc.AddCommentAsync(knowledge.Id, "Second", "B", null, null, CancellationToken.None);
        await _svc.AddCommentAsync(knowledge.Id, "Third", "C", null, null, CancellationToken.None);

        var result = await _svc.ListCommentsAsync(knowledge.Id, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal("First", result[0].Body);
        Assert.Equal("Second", result[1].Body);
        Assert.Equal("Third", result[2].Body);
    }

    [Fact]
    public async Task ListComments_ExcludesSoftDeletedComments()
    {
        var knowledge = await SeedKnowledge();
        var comment = await _svc.AddCommentAsync(
            knowledge.Id, "Will be deleted", "Alice", null, null, CancellationToken.None);
        await _svc.AddCommentAsync(
            knowledge.Id, "Stays", "Bob", null, null, CancellationToken.None);

        await _svc.DeleteCommentAsync(comment!.Id, CancellationToken.None);

        var result = await _svc.ListCommentsAsync(knowledge.Id, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Stays", result[0].Body);
    }

    [Fact]
    public async Task ListComments_PopulatesAttachmentCount()
    {
        var knowledge = await SeedKnowledge();
        var comment = await _svc.AddCommentAsync(
            knowledge.Id, "With attachments", "Alice", null, null, CancellationToken.None);

        // Add file attachments
        var fileRecord = new FileRecord
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            FileName = "file.txt",
            ContentType = "text/plain"
        };
        _db.FileRecords.Add(fileRecord);
        _db.FileAttachments.Add(new FileAttachment
        {
            Id = Guid.NewGuid(),
            FileRecordId = fileRecord.Id,
            CommentId = comment!.Id,
            TenantId = TenantId
        });
        _db.FileAttachments.Add(new FileAttachment
        {
            Id = Guid.NewGuid(),
            FileRecordId = fileRecord.Id,
            CommentId = comment.Id,
            TenantId = TenantId
        });
        await _db.SaveChangesAsync();

        var result = await _svc.ListCommentsAsync(knowledge.Id, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(2, result[0].AttachmentCount);
    }

    // =============================================
    // SVC_CommentService: GetCommentAsync
    // =============================================

    [Fact]
    public async Task GetComment_ReturnsComment_WhenExists()
    {
        var knowledge = await SeedKnowledge();
        var created = await _svc.AddCommentAsync(
            knowledge.Id, "Get me", "Alice", null, null, CancellationToken.None);

        var result = await _svc.GetCommentAsync(created!.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("Get me", result.Body);
    }

    [Fact]
    public async Task GetComment_ReturnsNull_WhenNotFound()
    {
        var result = await _svc.GetCommentAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    // =============================================
    // SVC_CommentService: UpdateCommentAsync
    // =============================================

    [Fact]
    public async Task UpdateComment_UpdatesBody()
    {
        var knowledge = await SeedKnowledge();
        var created = await _svc.AddCommentAsync(
            knowledge.Id, "Original", "Alice", null, null, CancellationToken.None);

        var result = await _svc.UpdateCommentAsync(
            created!.Id, "Updated body", null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Updated body", result.Body);
    }

    [Fact]
    public async Task UpdateComment_UpdatesSentiment()
    {
        var knowledge = await SeedKnowledge();
        var created = await _svc.AddCommentAsync(
            knowledge.Id, "Body", "Alice", null, null, CancellationToken.None);

        var result = await _svc.UpdateCommentAsync(
            created!.Id, null, "negative", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("negative", result.Sentiment);
        Assert.Equal("Body", result.Body); // Body unchanged
    }

    [Fact]
    public async Task UpdateComment_SetsUpdatedAt()
    {
        var knowledge = await SeedKnowledge();
        var created = await _svc.AddCommentAsync(
            knowledge.Id, "Body", "Alice", null, null, CancellationToken.None);
        var beforeUpdate = DateTime.UtcNow;

        await Task.Delay(10);
        var result = await _svc.UpdateCommentAsync(
            created!.Id, "New body", null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.UpdatedAt >= beforeUpdate);
    }

    [Fact]
    public async Task UpdateComment_TriggersEnrichment_WhenBodyChanged()
    {
        var knowledge = await SeedKnowledge();
        var created = await _svc.AddCommentAsync(
            knowledge.Id, "Original", "Alice", null, null, CancellationToken.None);

        _enrichmentWriter.ClearReceivedCalls();

        await _svc.UpdateCommentAsync(
            created!.Id, "Updated body", null, CancellationToken.None);

        await _enrichmentWriter.Received(1).EnqueueAsync(
            knowledge.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateComment_DoesNotTriggerEnrichment_WhenOnlySentimentChanged()
    {
        var knowledge = await SeedKnowledge();
        var created = await _svc.AddCommentAsync(
            knowledge.Id, "Body", "Alice", null, null, CancellationToken.None);

        _enrichmentWriter.ClearReceivedCalls();

        await _svc.UpdateCommentAsync(
            created!.Id, null, "positive", CancellationToken.None);

        await _enrichmentWriter.DidNotReceive().EnqueueAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateComment_ReturnsNull_WhenNotFound()
    {
        var result = await _svc.UpdateCommentAsync(
            Guid.NewGuid(), "Body", null, CancellationToken.None);

        Assert.Null(result);
    }

    // =============================================
    // SVC_CommentService: DeleteCommentAsync
    // =============================================

    [Fact]
    public async Task DeleteComment_SoftDeletesComment()
    {
        var knowledge = await SeedKnowledge();
        var created = await _svc.AddCommentAsync(
            knowledge.Id, "Delete me", "Alice", null, null, CancellationToken.None);

        var result = await _svc.DeleteCommentAsync(created!.Id, CancellationToken.None);

        Assert.True(result);

        // Verify soft-deleted (query filter excludes it from queries)
        var found = await _db.Comments
            .FirstOrDefaultAsync(c => c.Id == created.Id);
        Assert.Null(found);

        // But exists when ignoring query filters
        var raw = await _db.Comments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == created.Id);
        Assert.NotNull(raw);
        Assert.True(raw.IsDeleted);
    }

    [Fact]
    public async Task DeleteComment_CascadesSoftDeleteToReplies()
    {
        var knowledge = await SeedKnowledge();
        var parent = await _svc.AddCommentAsync(
            knowledge.Id, "Parent", "Alice", null, null, CancellationToken.None);
        var reply = await _svc.AddCommentAsync(
            knowledge.Id, "Reply", "Bob", parent!.Id, null, CancellationToken.None);

        await _svc.DeleteCommentAsync(parent.Id, CancellationToken.None);

        // Both parent and reply should be soft-deleted
        var parentRaw = await _db.Comments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == parent.Id);
        var replyRaw = await _db.Comments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == reply!.Id);
        Assert.True(parentRaw!.IsDeleted);
        Assert.True(replyRaw!.IsDeleted);
    }

    [Fact]
    public async Task DeleteComment_RemovesFileAttachments()
    {
        var knowledge = await SeedKnowledge();
        var created = await _svc.AddCommentAsync(
            knowledge.Id, "With files", "Alice", null, null, CancellationToken.None);

        // Add file attachment
        var fileRecord = new FileRecord
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            FileName = "file.txt",
            ContentType = "text/plain"
        };
        _db.FileRecords.Add(fileRecord);
        var attachment = new FileAttachment
        {
            Id = Guid.NewGuid(),
            FileRecordId = fileRecord.Id,
            CommentId = created!.Id,
            TenantId = TenantId
        };
        _db.FileAttachments.Add(attachment);
        await _db.SaveChangesAsync();

        await _svc.DeleteCommentAsync(created.Id, CancellationToken.None);

        // FileAttachment junction row should be hard-deleted
        var remaining = await _db.FileAttachments
            .Where(fa => fa.CommentId == created.Id)
            .CountAsync();
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task DeleteComment_TriggersEnrichment()
    {
        var knowledge = await SeedKnowledge();
        var created = await _svc.AddCommentAsync(
            knowledge.Id, "Body", "Alice", null, null, CancellationToken.None);

        _enrichmentWriter.ClearReceivedCalls();

        await _svc.DeleteCommentAsync(created!.Id, CancellationToken.None);

        await _enrichmentWriter.Received(1).EnqueueAsync(
            knowledge.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteComment_ReturnsFalse_WhenNotFound()
    {
        var result = await _svc.DeleteCommentAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result);
    }

    // =============================================
    // Tenant Isolation
    // =============================================

    [Fact]
    public async Task TenantIsolation_CannotAccessCommentsFromOtherTenant()
    {
        var knowledge = await SeedKnowledge();

        // Directly insert a comment for a different tenant
        _db.Comments.Add(new KnowledgeComment
        {
            TenantId = OtherTenantId,
            KnowledgeId = knowledge.Id,
            AuthorName = "OtherTenant",
            Body = "Other tenant comment"
        });
        await _db.SaveChangesAsync();

        var result = await _svc.ListCommentsAsync(knowledge.Id, CancellationToken.None);

        Assert.DoesNotContain(result, c => c.AuthorName == "OtherTenant");
    }

    // =============================================
    // MOD_SearchWithComments: Re-enrichment triggers
    // =============================================

    [Fact]
    public async Task EnrichmentTriggered_OnAddComment()
    {
        var knowledge = await SeedKnowledge();
        _enrichmentWriter.ClearReceivedCalls();

        await _svc.AddCommentAsync(
            knowledge.Id, "New comment", "Alice", null, null, CancellationToken.None);

        await _enrichmentWriter.Received(1).EnqueueAsync(
            knowledge.Id, TenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichmentTriggered_OnUpdateCommentBody()
    {
        var knowledge = await SeedKnowledge();
        var comment = await _svc.AddCommentAsync(
            knowledge.Id, "Original", "Alice", null, null, CancellationToken.None);
        _enrichmentWriter.ClearReceivedCalls();

        await _svc.UpdateCommentAsync(comment!.Id, "Updated", null, CancellationToken.None);

        await _enrichmentWriter.Received(1).EnqueueAsync(
            knowledge.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichmentNotTriggered_OnSentimentOnlyUpdate()
    {
        var knowledge = await SeedKnowledge();
        var comment = await _svc.AddCommentAsync(
            knowledge.Id, "Body", "Alice", null, null, CancellationToken.None);
        _enrichmentWriter.ClearReceivedCalls();

        await _svc.UpdateCommentAsync(comment!.Id, null, "neutral", CancellationToken.None);

        await _enrichmentWriter.DidNotReceive().EnqueueAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichmentTriggered_OnDeleteComment()
    {
        var knowledge = await SeedKnowledge();
        var comment = await _svc.AddCommentAsync(
            knowledge.Id, "Delete me", "Alice", null, null, CancellationToken.None);
        _enrichmentWriter.ClearReceivedCalls();

        await _svc.DeleteCommentAsync(comment!.Id, CancellationToken.None);

        await _enrichmentWriter.Received(1).EnqueueAsync(
            knowledge.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // =============================================
    // MOD_SearchWithComments: Enrichment null safety
    // =============================================

    [Fact]
    public async Task AddComment_WorksWithoutEnrichmentWriter()
    {
        var options = new DbContextOptionsBuilder<SelfHostedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(TenantId);
        using var db = new SelfHostedDbContext(options, tenantProvider);
        var logger = Substitute.For<ILogger<CommentService>>();
        var svcNoEnrichment = new CommentService(db, tenantProvider, logger); // no enrichmentWriter

        var knowledge = new Knowledge { TenantId = TenantId, Title = "Test", Content = "C" };
        db.KnowledgeItems.Add(knowledge);
        await db.SaveChangesAsync();

        var result = await svcNoEnrichment.AddCommentAsync(
            knowledge.Id, "Body", "Author", null, null, CancellationToken.None);

        Assert.NotNull(result);
        db.Database.EnsureDeleted();
    }
}
