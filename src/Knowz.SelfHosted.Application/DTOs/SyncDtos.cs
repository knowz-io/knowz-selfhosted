namespace Knowz.SelfHosted.Application.DTOs;

/// <summary>
/// Direction for vault sync operations.
/// </summary>
public enum SyncDirection
{
    Full = 0,
    PullOnly = 1,
    PushOnly = 2
}

/// <summary>
/// Result from a vault sync operation.
/// </summary>
public class VaultSyncResult
{
    public bool Success { get; set; }
    public SyncDirection Direction { get; set; }
    public int PullAccepted { get; set; }
    public int PullSkipped { get; set; }
    public int PushAccepted { get; set; }
    public int PushSkipped { get; set; }
    public int TombstonesApplied { get; set; }
    public List<string> Details { get; set; } = new();
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Status of a vault sync link.
/// </summary>
public class VaultSyncStatusDto
{
    public Guid LinkId { get; set; }
    public Guid LocalVaultId { get; set; }
    public string LocalVaultName { get; set; } = string.Empty;
    public Guid RemoteVaultId { get; set; }
    public string PlatformApiUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? LastSyncError { get; set; }
    public DateTime? LastSyncCompletedAt { get; set; }
    public DateTime? LastPullCursor { get; set; }
    public DateTime? LastPushCursor { get; set; }
    public bool SyncEnabled { get; set; }
}

/// <summary>
/// Result from importing a sync delta (last-write-wins).
/// </summary>
public class SyncDeltaImportResult
{
    public bool Success { get; set; }
    public int Accepted { get; set; }
    public int Skipped { get; set; }
    public int TombstonesApplied { get; set; }
    public List<string> Details { get; set; } = new();
    public string? Error { get; set; }
}

/// <summary>
/// Generic API response wrapper matching platform's ApiResponse{T} shape.
/// Used for deserializing platform sync API responses.
/// </summary>
public class PlatformApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Platform sync import response (mirrors Knowz.Shared.DTOs.VaultSync.VaultSyncImportResponse).
/// </summary>
public class PlatformImportResponse
{
    public bool Success { get; set; }
    public int Accepted { get; set; }
    public int Skipped { get; set; }
    public List<string> Details { get; set; } = new();
    public DateTime ServerTimestamp { get; set; }
}

/// <summary>
/// Platform schema version response.
/// </summary>
public class PlatformSchemaResponse
{
    public int Version { get; set; }
    public int MinReadableVersion { get; set; }
    public string Compatibility { get; set; } = string.Empty;
}

/// <summary>
/// Request to establish a sync link.
/// </summary>
public class EstablishSyncLinkRequest
{
    public Guid LocalVaultId { get; set; }
    public Guid RemoteVaultId { get; set; }
    public string PlatformApiUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Request to trigger a sync operation.
/// </summary>
public class TriggerSyncRequest
{
    public SyncDirection Direction { get; set; } = SyncDirection.Full;
    public bool DryRun { get; set; }
}
