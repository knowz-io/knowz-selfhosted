namespace Knowz.SelfHosted.Application.Interfaces;

using Knowz.SelfHosted.Application.DTOs;

/// <summary>
/// Orchestrates bidirectional vault sync between selfhosted and platform.
/// </summary>
public interface IVaultSyncOrchestrator
{
    /// <summary>
    /// Execute a sync operation for a linked vault.
    /// </summary>
    Task<VaultSyncResult> SyncAsync(Guid localVaultId, SyncDirection direction = SyncDirection.Full, CancellationToken ct = default);

    /// <summary>
    /// Get the sync status for a linked vault.
    /// </summary>
    Task<VaultSyncStatusDto?> GetStatusAsync(Guid localVaultId, CancellationToken ct = default);

    /// <summary>
    /// List all vault sync links.
    /// </summary>
    Task<List<VaultSyncStatusDto>> ListLinksAsync(CancellationToken ct = default);

    /// <summary>
    /// Establish a new sync link between a local vault and a platform vault.
    /// </summary>
    Task<VaultSyncStatusDto> EstablishLinkAsync(EstablishSyncLinkRequest request, CancellationToken ct = default);

    /// <summary>
    /// Remove a sync link.
    /// </summary>
    Task<bool> RemoveLinkAsync(Guid localVaultId, CancellationToken ct = default);
}
