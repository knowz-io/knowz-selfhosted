using Microsoft.Extensions.Configuration;

namespace Knowz.Core.Interfaces;

/// <summary>
/// Service for managing system configuration entries in self-hosted deployments.
/// Provides CRUD operations with encryption, masking, and config reload support.
/// </summary>
public interface IConfigurationManagementService
{
    Task<List<ConfigCategoryDto>> GetAllCategoriesAsync();
    Task<ConfigCategoryDto?> GetCategoryAsync(string category);
    Task<ConfigUpdateResult> UpdateCategoryAsync(string category, List<ConfigEntryUpdateDto> entries, string modifiedBy);
    Task<ServiceHealthResult> TestConnectionAsync(string category);
    Task<List<ServiceHealthResult>> TestAllConnectionsAsync();
    Task SeedFromConfigurationAsync(IConfiguration configuration);
    DeploymentStatusDto GetDeploymentStatus();
}

public class ConfigCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresRestart { get; set; }
    public List<ConfigEntryDto> Entries { get; set; } = new();
}

public class ConfigEntryDto
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public bool IsSecret { get; set; }
    public bool RequiresRestart { get; set; }
    public string? Description { get; set; }
    public bool IsSet { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? Source { get; set; } // "database", "keyvault", "environment", "appsettings", null
}

public class ConfigEntryUpdateDto
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public class ConfigUpdateResult
{
    public bool Success { get; set; }
    public bool RestartRequired { get; set; }
    public List<string> Errors { get; set; } = new();
    public int EntriesUpdated { get; set; }
}

public class ServiceHealthResult
{
    public string Category { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? LatencyMs { get; set; }
}

public class DeploymentStatusDto
{
    public string Mode { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime StartupTime { get; set; }
    public bool RestartRequired { get; set; }
    public List<string> RestartReasons { get; set; } = new();
}
