using Knowz.SelfHosted.Infrastructure.Data;
using Knowz.SelfHosted.Infrastructure.Data.Entities;
using Knowz.SelfHosted.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Knowz.SelfHosted.Application.Services;

public class PromptSeedingService
{
    private readonly SelfHostedDbContext _db;
    private readonly ILogger<PromptSeedingService> _logger;

    public PromptSeedingService(SelfHostedDbContext db, ILogger<PromptSeedingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Seeds the 6 platform-level default prompts if none exist yet. Idempotent.
    /// </summary>
    public async Task SeedDefaultPromptsAsync(CancellationToken ct = default)
    {
        var alreadySeeded = await _db.PromptTemplates
            .AnyAsync(pt => pt.Scope == PromptScope.Platform, ct);

        if (alreadySeeded)
        {
            _logger.LogDebug("Platform prompts already seeded — skipping");
            return;
        }

        _logger.LogInformation("Seeding default platform prompts...");

        var defaults = new Dictionary<string, string>
        {
            [PromptKeys.SystemPrompt] = DefaultPrompts.SystemPrompt,
            [PromptKeys.TitlePrompt] = DefaultPrompts.TitlePrompt,
            [PromptKeys.SummarizePrompt] = DefaultPrompts.SummarizePrompt,
            [PromptKeys.TagsPrompt] = DefaultPrompts.TagsPrompt,
            [PromptKeys.DocumentEditorPrompt] = DefaultPrompts.DocumentEditorPrompt,
            [PromptKeys.NoContextResponse] = DefaultPrompts.NoContextResponse,
        };

        foreach (var (key, text) in defaults)
        {
            _db.PromptTemplates.Add(new PromptTemplate
            {
                PromptKey = key,
                Scope = PromptScope.Platform,
                TenantId = null,
                UserId = null,
                TemplateText = text,
                MergeStrategy = PromptMergeStrategy.Override,
                Description = $"Default {key} prompt",
                IsSystemSeeded = true,
                LastModifiedBy = "system-seed",
            });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} default platform prompts", defaults.Count);
    }
}
