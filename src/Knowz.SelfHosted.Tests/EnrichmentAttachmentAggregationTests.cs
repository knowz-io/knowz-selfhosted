using System.Threading.Channels;
using Knowz.Core.Entities;
using Knowz.Core.Interfaces;
using Knowz.SelfHosted.Infrastructure.Data;
using Knowz.SelfHosted.Infrastructure.Data.Entities;
using Knowz.SelfHosted.Infrastructure.Interfaces;
using Knowz.SelfHosted.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Knowz.SelfHosted.Tests;

/// <summary>
/// Tests for GetAllAttachmentTextAsync and attachment inclusion in enrichment.
/// Covers VERIFY_IDX_01-07, VERIFY_SUM_01-04.
/// </summary>
public class EnrichmentAttachmentAggregationTests : IDisposable
{
    private readonly Channel<EnrichmentWorkItem> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceProvider _serviceProvider;
    private readonly ITextEnrichmentService _enrichmentService;
    private readonly ISearchService _searchService;
    private readonly IOpenAIService _openAIService;
    private readonly EnrichmentBackgroundService _svc;
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public EnrichmentAttachmentAggregationTests()
    {
        _channel = Channel.CreateBounded<EnrichmentWorkItem>(
            new BoundedChannelOptions(100) { SingleReader = true });

        var dbName = Guid.NewGuid().ToString();

        _enrichmentService = Substitute.For<ITextEnrichmentService>();
        _searchService = Substitute.For<ISearchService>();
        _openAIService = Substitute.For<IOpenAIService>();
        _openAIService.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[] { 0.1f, 0.2f, 0.3f });

        var services = new ServiceCollection();
        services.AddScoped<ITenantProvider>(sp =>
        {
            var tp = Substitute.For<ITenantProvider>();
            tp.TenantId.Returns(_ => TenantContext.CurrentTenantId ?? TenantId);
            return tp;
        });
        services.AddDbContext<SelfHostedDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName));
        services.AddScoped<ITextEnrichmentService>(_ => _enrichmentService);
        services.AddScoped<ISearchService>(_ => _searchService);
        services.AddScoped<IOpenAIService>(_ => _openAIService);
        services.AddScoped<ISelfHostedChunkingService, SelfHostedChunkingService>();

        _serviceProvider = services.BuildServiceProvider();
        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var logger = Substitute.For<ILogger<EnrichmentBackgroundService>>();
        _svc = new EnrichmentBackgroundService(_channel, _scopeFactory, logger);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private SelfHostedDbContext GetDb()
    {
        var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
    }

    // =============================================
    // GetAllAttachmentTextAsync
    // =============================================

    [Fact]
    public async Task GetAllAttachmentText_ReturnsKnowledgeLevelAttachments()
    {
        // VERIFY_IDX_01: Attachment text appears in aggregation
        var knowledgeId = Guid.NewGuid();
        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId, TenantId = TenantId,
                Title = "Test", Content = "Main content"
            });
            var fileRecord = new FileRecord
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                FileName = "notes.txt", ContentType = "text/plain",
                ExtractedText = "Important notes from file"
            };
            db.FileRecords.Add(fileRecord);
            db.FileAttachments.Add(new FileAttachment
            {
                Id = Guid.NewGuid(), FileRecordId = fileRecord.Id,
                KnowledgeId = knowledgeId, TenantId = TenantId
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            var result = await EnrichmentBackgroundService.GetAllAttachmentTextAsync(db, knowledgeId, CancellationToken.None);
            TenantContext.CurrentTenantId = null;

            Assert.Contains("notes.txt", result);
            Assert.Contains("Important notes from file", result);
        }
    }

    [Fact]
    public async Task GetAllAttachmentText_IncludesCommentBodies()
    {
        // VERIFY_IDX_04: Comment body text included
        var knowledgeId = Guid.NewGuid();
        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId, TenantId = TenantId,
                Title = "Test", Content = "Main"
            });
            db.Comments.Add(new KnowledgeComment
            {
                Id = Guid.NewGuid(), KnowledgeId = knowledgeId,
                TenantId = TenantId, AuthorName = "Alice",
                Body = "This is a valuable insight"
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            var result = await EnrichmentBackgroundService.GetAllAttachmentTextAsync(db, knowledgeId, CancellationToken.None);
            TenantContext.CurrentTenantId = null;

            Assert.Contains("Alice", result);
            Assert.Contains("This is a valuable insight", result);
        }
    }

    [Fact]
    public async Task GetAllAttachmentText_IncludesCommentLevelAttachments()
    {
        // VERIFY_IDX_03: Comment-level attachment text included
        var knowledgeId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId, TenantId = TenantId,
                Title = "Test", Content = "Main"
            });
            db.Comments.Add(new KnowledgeComment
            {
                Id = commentId, KnowledgeId = knowledgeId,
                TenantId = TenantId, AuthorName = "Bob",
                Body = "See the attachment"
            });
            var fileRecord = new FileRecord
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                FileName = "report.csv", ContentType = "text/csv",
                ExtractedText = "name,value\nAlpha,100"
            };
            db.FileRecords.Add(fileRecord);
            db.FileAttachments.Add(new FileAttachment
            {
                Id = Guid.NewGuid(), FileRecordId = fileRecord.Id,
                CommentId = commentId, TenantId = TenantId
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            var result = await EnrichmentBackgroundService.GetAllAttachmentTextAsync(db, knowledgeId, CancellationToken.None);
            TenantContext.CurrentTenantId = null;

            Assert.Contains("report.csv", result);
            Assert.Contains("name,value", result);
        }
    }

    [Fact]
    public async Task GetAllAttachmentText_SkipsNonImageNullExtractedText()
    {
        // VERIFY_IDX_06: Non-image attachments with null ExtractedText are skipped
        var knowledgeId = Guid.NewGuid();
        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId, TenantId = TenantId,
                Title = "Test", Content = "Main"
            });
            var fileWithText = new FileRecord
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                FileName = "with-text.txt", ExtractedText = "has content"
            };
            var fileWithout = new FileRecord
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                FileName = "no-text.pdf", ContentType = "application/pdf", ExtractedText = null
            };
            db.FileRecords.AddRange(fileWithText, fileWithout);
            db.FileAttachments.Add(new FileAttachment
            {
                Id = Guid.NewGuid(), FileRecordId = fileWithText.Id,
                KnowledgeId = knowledgeId, TenantId = TenantId
            });
            db.FileAttachments.Add(new FileAttachment
            {
                Id = Guid.NewGuid(), FileRecordId = fileWithout.Id,
                KnowledgeId = knowledgeId, TenantId = TenantId
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            var result = await EnrichmentBackgroundService.GetAllAttachmentTextAsync(db, knowledgeId, CancellationToken.None);
            TenantContext.CurrentTenantId = null;

            Assert.Contains("with-text.txt", result);
            Assert.DoesNotContain("no-text.pdf", result);
        }
    }

    [Fact]
    public async Task GetAllAttachmentText_IncludesImagePlaceholder_WhenExtractedTextNull()
    {
        // Image files with null ExtractedText should get a placeholder note
        var knowledgeId = Guid.NewGuid();
        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId, TenantId = TenantId,
                Title = "Test", Content = "Main"
            });
            var imageFile = new FileRecord
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                FileName = "photo.jpg", ContentType = "image/jpeg", ExtractedText = null
            };
            db.FileRecords.Add(imageFile);
            db.FileAttachments.Add(new FileAttachment
            {
                Id = Guid.NewGuid(), FileRecordId = imageFile.Id,
                KnowledgeId = knowledgeId, TenantId = TenantId
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            var result = await EnrichmentBackgroundService.GetAllAttachmentTextAsync(db, knowledgeId, CancellationToken.None);
            TenantContext.CurrentTenantId = null;

            Assert.Contains("photo.jpg", result);
            Assert.Contains("[Image: photo.jpg", result);
            Assert.Contains("not yet analyzed", result);
        }
    }

    [Fact]
    public async Task GetAllAttachmentText_IncludesImagePlaceholder_ForPngContentType()
    {
        // All image/* content types should get placeholder treatment
        var knowledgeId = Guid.NewGuid();
        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId, TenantId = TenantId,
                Title = "Test", Content = "Main"
            });
            var imageFile = new FileRecord
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                FileName = "screenshot.png", ContentType = "image/png", ExtractedText = null
            };
            db.FileRecords.Add(imageFile);
            db.FileAttachments.Add(new FileAttachment
            {
                Id = Guid.NewGuid(), FileRecordId = imageFile.Id,
                KnowledgeId = knowledgeId, TenantId = TenantId
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            var result = await EnrichmentBackgroundService.GetAllAttachmentTextAsync(db, knowledgeId, CancellationToken.None);
            TenantContext.CurrentTenantId = null;

            Assert.Contains("screenshot.png", result);
            Assert.Contains("[Image: screenshot.png", result);
        }
    }

    [Fact]
    public async Task GetAllAttachmentText_UsesExtractedText_WhenImageHasIt()
    {
        // Image file WITH ExtractedText should use that text, not the placeholder
        var knowledgeId = Guid.NewGuid();
        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId, TenantId = TenantId,
                Title = "Test", Content = "Main"
            });
            var imageFile = new FileRecord
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                FileName = "analyzed.jpg", ContentType = "image/jpeg",
                ExtractedText = "A cat sitting on a windowsill"
            };
            db.FileRecords.Add(imageFile);
            db.FileAttachments.Add(new FileAttachment
            {
                Id = Guid.NewGuid(), FileRecordId = imageFile.Id,
                KnowledgeId = knowledgeId, TenantId = TenantId
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            var result = await EnrichmentBackgroundService.GetAllAttachmentTextAsync(db, knowledgeId, CancellationToken.None);
            TenantContext.CurrentTenantId = null;

            Assert.Contains("analyzed.jpg", result);
            Assert.Contains("A cat sitting on a windowsill", result);
            Assert.DoesNotContain("not yet analyzed", result);
        }
    }

    [Fact]
    public async Task GetAllAttachmentText_IncludesCommentImagePlaceholder_WhenExtractedTextNull()
    {
        // Comment-level image attachments with null ExtractedText should also get placeholder
        var knowledgeId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId, TenantId = TenantId,
                Title = "Test", Content = "Main"
            });
            db.Comments.Add(new KnowledgeComment
            {
                Id = commentId, KnowledgeId = knowledgeId,
                TenantId = TenantId, AuthorName = "Alice",
                Body = "See attached image"
            });
            var imageFile = new FileRecord
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                FileName = "diagram.png", ContentType = "image/png", ExtractedText = null
            };
            db.FileRecords.Add(imageFile);
            db.FileAttachments.Add(new FileAttachment
            {
                Id = Guid.NewGuid(), FileRecordId = imageFile.Id,
                CommentId = commentId, TenantId = TenantId
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            var result = await EnrichmentBackgroundService.GetAllAttachmentTextAsync(db, knowledgeId, CancellationToken.None);
            TenantContext.CurrentTenantId = null;

            Assert.Contains("diagram.png", result);
            Assert.Contains("[Image: diagram.png", result);
            Assert.Contains("not yet analyzed", result);
        }
    }

    [Fact]
    public async Task GetAllAttachmentText_ReturnsEmpty_WhenNoAttachments()
    {
        // VERIFY_SUM_04: No attachments = empty string
        var knowledgeId = Guid.NewGuid();
        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId, TenantId = TenantId,
                Title = "Test", Content = "Just content"
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            var result = await EnrichmentBackgroundService.GetAllAttachmentTextAsync(db, knowledgeId, CancellationToken.None);
            TenantContext.CurrentTenantId = null;

            Assert.Equal(string.Empty, result);
        }
    }

    [Fact]
    public async Task GetAllAttachmentText_RespectsMaxCharsLimit()
    {
        // VERIFY_IDX_05: Aggregated content respects 50K char limit
        var knowledgeId = Guid.NewGuid();
        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId, TenantId = TenantId,
                Title = "Test", Content = "Main"
            });

            // Create 10 attachments each with ~10K chars = 100K > 50K limit
            for (int i = 0; i < 10; i++)
            {
                var fr = new FileRecord
                {
                    Id = Guid.NewGuid(), TenantId = TenantId,
                    FileName = $"file{i}.txt",
                    ExtractedText = new string('x', 10_000)
                };
                db.FileRecords.Add(fr);
                db.FileAttachments.Add(new FileAttachment
                {
                    Id = Guid.NewGuid(), FileRecordId = fr.Id,
                    KnowledgeId = knowledgeId, TenantId = TenantId
                });
            }
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            var result = await EnrichmentBackgroundService.GetAllAttachmentTextAsync(db, knowledgeId, CancellationToken.None);
            TenantContext.CurrentTenantId = null;

            // Not all 10 files should be included due to 50K limit
            // Each section is ~10K + separator overhead, so around 5 should be included
            Assert.True(result.Length < 100_000, "Should be truncated by 50K char limit");
        }
    }

    [Fact]
    public async Task GetAllAttachmentText_ExcludesSoftDeletedFiles()
    {
        var knowledgeId = Guid.NewGuid();
        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId, TenantId = TenantId,
                Title = "Test", Content = "Main"
            });
            var active = new FileRecord
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                FileName = "active.txt", ExtractedText = "Active content"
            };
            var deleted = new FileRecord
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                FileName = "deleted.txt", ExtractedText = "Deleted content",
                IsDeleted = true
            };
            db.FileRecords.AddRange(active, deleted);
            db.FileAttachments.Add(new FileAttachment
            {
                Id = Guid.NewGuid(), FileRecordId = active.Id,
                KnowledgeId = knowledgeId, TenantId = TenantId
            });
            db.FileAttachments.Add(new FileAttachment
            {
                Id = Guid.NewGuid(), FileRecordId = deleted.Id,
                KnowledgeId = knowledgeId, TenantId = TenantId
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        using (var db = GetDb())
        {
            TenantContext.CurrentTenantId = TenantId;
            var result = await EnrichmentBackgroundService.GetAllAttachmentTextAsync(db, knowledgeId, CancellationToken.None);
            TenantContext.CurrentTenantId = null;

            Assert.Contains("active.txt", result);
            Assert.DoesNotContain("deleted.txt", result);
        }
    }

    // =============================================
    // ProcessWorkItemAsync with attachments (VERIFY_SUM_02)
    // =============================================

    [Fact]
    public async Task ProcessWorkItem_IncludesAttachmentTextInSummary()
    {
        // VERIFY_SUM_02: SummarizeAsync receives aggregated content
        var knowledgeId = Guid.NewGuid();
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId, TenantId = TenantId,
                Title = "Good Title", Content = "Main knowledge content"
            });
            var fr = new FileRecord
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                FileName = "data.txt",
                ExtractedText = "Attachment-specific data about quantum computing"
            };
            db.FileRecords.Add(fr);
            db.FileAttachments.Add(new FileAttachment
            {
                Id = Guid.NewGuid(), FileRecordId = fr.Id,
                KnowledgeId = knowledgeId, TenantId = TenantId
            });
            db.EnrichmentOutbox.Add(new EnrichmentOutboxItem
            {
                TenantId = TenantId, KnowledgeId = knowledgeId,
                Status = EnrichmentStatus.Pending
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        _enrichmentService.SummarizeAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Guid?>())
            .Returns("Summary with attachments");
        _enrichmentService.ExtractTagsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Guid?>())
            .Returns(new List<string>());

        await _svc.ProcessWorkItemAsync(new EnrichmentWorkItem(knowledgeId, TenantId), CancellationToken.None);

        // Verify SummarizeAsync was called with content that includes attachment text
        await _enrichmentService.Received(1).SummarizeAsync(
            Arg.Is<string>(s => s.Contains("Main knowledge content") && s.Contains("quantum computing")),
            Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Guid?>());
    }

    [Fact]
    public async Task ProcessWorkItem_UsesKnowledgeContentOnly_WhenNoAttachments()
    {
        // VERIFY_SUM_04: No attachments = knowledge content only
        var knowledgeId = Guid.NewGuid();
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId, TenantId = TenantId,
                Title = "Good Title", Content = "Only this content that needs summarization by the enrichment service and it must be long enough to exceed the short content threshold of twenty words for proper testing"
            });
            db.EnrichmentOutbox.Add(new EnrichmentOutboxItem
            {
                TenantId = TenantId, KnowledgeId = knowledgeId,
                Status = EnrichmentStatus.Pending
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        _enrichmentService.SummarizeAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Guid?>())
            .Returns("Summary");
        _enrichmentService.ExtractTagsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Guid?>())
            .Returns(new List<string>());

        await _svc.ProcessWorkItemAsync(new EnrichmentWorkItem(knowledgeId, TenantId), CancellationToken.None);

        // Verify SummarizeAsync was called with just knowledge content
        await _enrichmentService.Received(1).SummarizeAsync(
            "Only this content that needs summarization by the enrichment service and it must be long enough to exceed the short content threshold of twenty words for proper testing",
            Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Guid?>());
    }

    [Fact]
    public async Task ProcessWorkItem_IncludesAttachmentTextInTagExtraction()
    {
        // VERIFY_SUM_03: Tag extraction also receives aggregated content
        var knowledgeId = Guid.NewGuid();
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId, TenantId = TenantId,
                Title = "Good Title", Content = "Main content"
            });
            var fr = new FileRecord
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                FileName = "data.txt",
                ExtractedText = "Attachment about artificial intelligence"
            };
            db.FileRecords.Add(fr);
            db.FileAttachments.Add(new FileAttachment
            {
                Id = Guid.NewGuid(), FileRecordId = fr.Id,
                KnowledgeId = knowledgeId, TenantId = TenantId
            });
            db.EnrichmentOutbox.Add(new EnrichmentOutboxItem
            {
                TenantId = TenantId, KnowledgeId = knowledgeId,
                Status = EnrichmentStatus.Pending
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        _enrichmentService.SummarizeAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Guid?>())
            .Returns((string?)null);
        _enrichmentService.ExtractTagsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Guid?>())
            .Returns(new List<string>());

        await _svc.ProcessWorkItemAsync(new EnrichmentWorkItem(knowledgeId, TenantId), CancellationToken.None);

        // Verify ExtractTagsAsync received content with attachment text
        await _enrichmentService.Received(1).ExtractTagsAsync(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("Main content") && s.Contains("artificial intelligence")),
            Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Guid?>());
    }
}
