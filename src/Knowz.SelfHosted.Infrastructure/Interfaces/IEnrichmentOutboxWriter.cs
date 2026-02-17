namespace Knowz.SelfHosted.Infrastructure.Interfaces;

/// <summary>
/// Enqueues knowledge items for background AI enrichment.
/// Writes to DB outbox (for durability) and Channel (for immediate processing).
/// </summary>
public interface IEnrichmentOutboxWriter
{
    /// <summary>
    /// Enqueues a knowledge item for enrichment.
    /// Deduplicates: skips if a Pending/Processing item already exists for this KnowledgeId.
    /// </summary>
    Task EnqueueAsync(Guid knowledgeId, Guid tenantId, CancellationToken ct = default);
}
