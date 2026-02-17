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

public class EnrichmentBackgroundServiceTests : IDisposable
{
    private readonly Channel<EnrichmentWorkItem> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceProvider _serviceProvider;
    private readonly SelfHostedDbContext _db;
    private readonly ITextEnrichmentService _enrichmentService;
    private readonly ISearchService _searchService;
    private readonly IOpenAIService _openAIService;
    private readonly EnrichmentBackgroundService _svc;
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public EnrichmentBackgroundServiceTests()
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

        // Get a reference DB for seeding
        using var seedScope = _scopeFactory.CreateScope();
        _db = seedScope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();

        var logger = Substitute.For<ILogger<EnrichmentBackgroundService>>();
        _svc = new EnrichmentBackgroundService(_channel, _scopeFactory, logger);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    // --- IsPlaceholderTitle tests ---

    [Theory]
    [InlineData(null, "any content", true)]
    [InlineData("", "any content", true)]
    [InlineData("   ", "any content", true)]
    [InlineData("Untitled", "any content", true)]
    [InlineData("untitled", "any content", true)]
    [InlineData("UNTITLED", "any content", true)]
    [InlineData("Untitled Content", "any content", true)]
    [InlineData("Media File", "any content", true)]
    [InlineData("Document", "any content", true)]
    [InlineData("file", "any content", true)]
    [InlineData("n/a", "any content", true)]
    [InlineData("unknown", "any content", true)]
    [InlineData("A Real Title", "any content", false)]
    [InlineData("My Important Document", "any content", false)]
    public void IsPlaceholderTitle_DetectsCorrectly(string? title, string? content, bool expected)
    {
        Assert.Equal(expected, EnrichmentBackgroundService.IsPlaceholderTitle(title, content));
    }

    [Fact]
    public void IsPlaceholderTitle_TruncatedBodyText_IsPlaceholder()
    {
        var content = new string('x', 200);
        var title = content[..80]; // First 80 chars
        Assert.True(EnrichmentBackgroundService.IsPlaceholderTitle(title, content));
    }

    [Fact]
    public void IsPlaceholderTitle_ShortContent_NotPlaceholder()
    {
        var content = "Short";
        var title = "Short";
        // Content < 80 chars, so truncated-body check doesn't apply
        Assert.False(EnrichmentBackgroundService.IsPlaceholderTitle(title, content));
    }

    // --- ProcessWorkItemAsync tests ---

    [Fact]
    public async Task ProcessWorkItem_SetsOutboxToCompleted_OnSuccess()
    {
        // Seed
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                TenantId = TenantId,
                Title = "Real Title",
                Content = "Some content to enrich"
            });
            db.EnrichmentOutbox.Add(new EnrichmentOutboxItem
            {
                TenantId = TenantId,
                KnowledgeId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Status = EnrichmentStatus.Pending
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        _enrichmentService.SummarizeAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns("A summary");
        _enrichmentService.ExtractTagsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "tag1" });

        var workItem = new EnrichmentWorkItem(
            Guid.Parse("11111111-1111-1111-1111-111111111111"), TenantId);

        await _svc.ProcessWorkItemAsync(workItem, CancellationToken.None);

        // Verify outbox item is completed
        using var verifyScope = _scopeFactory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
        var outbox = await verifyDb.EnrichmentOutbox.FirstAsync();
        Assert.Equal(EnrichmentStatus.Completed, outbox.Status);
        Assert.NotNull(outbox.ProcessedAt);
    }

    [Fact]
    public async Task ProcessWorkItem_SkipsTitleGeneration_WhenTitleIsNotPlaceholder()
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                TenantId = TenantId,
                Title = "A Real Title",
                Content = "Some content"
            });
            db.EnrichmentOutbox.Add(new EnrichmentOutboxItem
            {
                TenantId = TenantId,
                KnowledgeId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Status = EnrichmentStatus.Pending
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        _enrichmentService.SummarizeAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _enrichmentService.ExtractTagsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        await _svc.ProcessWorkItemAsync(
            new EnrichmentWorkItem(Guid.Parse("22222222-2222-2222-2222-222222222222"), TenantId),
            CancellationToken.None);

        // Title generation should NOT have been called
        await _enrichmentService.DidNotReceive()
            .GenerateTitleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessWorkItem_GeneratesTitle_WhenPlaceholder()
    {
        var knowledgeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId,
                TenantId = TenantId,
                Title = "Untitled",
                Content = "Content about machine learning"
            });
            db.EnrichmentOutbox.Add(new EnrichmentOutboxItem
            {
                TenantId = TenantId,
                KnowledgeId = knowledgeId,
                Status = EnrichmentStatus.Pending
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        _enrichmentService.GenerateTitleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Machine Learning Overview");
        _enrichmentService.SummarizeAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _enrichmentService.ExtractTagsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        await _svc.ProcessWorkItemAsync(new EnrichmentWorkItem(knowledgeId, TenantId), CancellationToken.None);

        // Verify title was updated
        using var verifyScope = _scopeFactory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
        TenantContext.CurrentTenantId = TenantId;
        var knowledge = await verifyDb.KnowledgeItems.FindAsync(knowledgeId);
        TenantContext.CurrentTenantId = null;
        Assert.Equal("Machine Learning Overview", knowledge!.Title);
    }

    [Fact]
    public async Task ProcessWorkItem_SetsOutboxToFailed_WhenKnowledgeNotFound()
    {
        var knowledgeId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
            db.EnrichmentOutbox.Add(new EnrichmentOutboxItem
            {
                TenantId = TenantId,
                KnowledgeId = knowledgeId,
                Status = EnrichmentStatus.Pending
            });
            await db.SaveChangesAsync();
        }

        await _svc.ProcessWorkItemAsync(new EnrichmentWorkItem(knowledgeId, TenantId), CancellationToken.None);

        using var verifyScope = _scopeFactory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
        var outbox = await verifyDb.EnrichmentOutbox.FirstAsync();
        Assert.Equal(EnrichmentStatus.Failed, outbox.Status);
        Assert.Equal("Knowledge item not found", outbox.ErrorMessage);
    }

    [Fact]
    public async Task ProcessWorkItem_IncreasesRetryCount_OnProcessingError()
    {
        var knowledgeId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId,
                TenantId = TenantId,
                Title = "Untitled",
                Content = "Content"
            });
            db.EnrichmentOutbox.Add(new EnrichmentOutboxItem
            {
                TenantId = TenantId,
                KnowledgeId = knowledgeId,
                Status = EnrichmentStatus.Pending
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        _enrichmentService.GenerateTitleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string?>(_ => throw new Exception("AI error"));

        await _svc.ProcessWorkItemAsync(new EnrichmentWorkItem(knowledgeId, TenantId), CancellationToken.None);

        using var verifyScope = _scopeFactory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
        var outbox = await verifyDb.EnrichmentOutbox.FirstAsync();
        Assert.Equal(1, outbox.RetryCount);
        Assert.Equal(EnrichmentStatus.Pending, outbox.Status); // Retryable
        Assert.NotNull(outbox.NextRetryAt);
        Assert.Contains("AI error", outbox.ErrorMessage);
    }

    [Fact]
    public async Task ProcessWorkItem_SetsToFailed_AfterMaxRetries()
    {
        var knowledgeId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId,
                TenantId = TenantId,
                Title = "Untitled",
                Content = "Content"
            });
            db.EnrichmentOutbox.Add(new EnrichmentOutboxItem
            {
                TenantId = TenantId,
                KnowledgeId = knowledgeId,
                Status = EnrichmentStatus.Pending,
                RetryCount = 2, // Already retried twice, next failure = 3 = max
                MaxRetries = 3
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        _enrichmentService.GenerateTitleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string?>(_ => throw new Exception("Persistent error"));

        await _svc.ProcessWorkItemAsync(new EnrichmentWorkItem(knowledgeId, TenantId), CancellationToken.None);

        using var verifyScope = _scopeFactory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
        var outbox = await verifyDb.EnrichmentOutbox.FirstAsync();
        Assert.Equal(3, outbox.RetryCount);
        Assert.Equal(EnrichmentStatus.Failed, outbox.Status);
    }

    [Fact]
    public async Task ProcessWorkItem_ClearsTenantContext_Always()
    {
        // Even if processing fails, TenantContext should be cleared
        var knowledgeId = Guid.NewGuid();

        await _svc.ProcessWorkItemAsync(new EnrichmentWorkItem(knowledgeId, TenantId), CancellationToken.None);

        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public async Task ProcessWorkItem_UpdatesSummary()
    {
        var knowledgeId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
            TenantContext.CurrentTenantId = TenantId;
            db.KnowledgeItems.Add(new Knowledge
            {
                Id = knowledgeId,
                TenantId = TenantId,
                Title = "Good Title",
                Content = "Long content about various topics"
            });
            db.EnrichmentOutbox.Add(new EnrichmentOutboxItem
            {
                TenantId = TenantId,
                KnowledgeId = knowledgeId,
                Status = EnrichmentStatus.Pending
            });
            await db.SaveChangesAsync();
            TenantContext.CurrentTenantId = null;
        }

        _enrichmentService.SummarizeAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns("This is a generated summary");
        _enrichmentService.ExtractTagsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        await _svc.ProcessWorkItemAsync(new EnrichmentWorkItem(knowledgeId, TenantId), CancellationToken.None);

        using var verifyScope = _scopeFactory.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<SelfHostedDbContext>();
        TenantContext.CurrentTenantId = TenantId;
        var knowledge = await verifyDb.KnowledgeItems.FindAsync(knowledgeId);
        TenantContext.CurrentTenantId = null;
        Assert.Equal("This is a generated summary", knowledge!.Summary);
    }

    // --- TenantContext tests ---

    [Fact]
    public void TenantContext_DefaultsToNull()
    {
        TenantContext.CurrentTenantId = null; // Reset
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public void TenantContext_SetAndGet()
    {
        var id = Guid.NewGuid();
        TenantContext.CurrentTenantId = id;
        Assert.Equal(id, TenantContext.CurrentTenantId);
        TenantContext.CurrentTenantId = null; // Cleanup
    }
}
