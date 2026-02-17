namespace Knowz.SelfHosted.Infrastructure.Data.Entities;

public class EnrichmentOutboxItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid KnowledgeId { get; set; }
    public EnrichmentStatus Status { get; set; } = EnrichmentStatus.Pending;
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
}

public enum EnrichmentStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}
