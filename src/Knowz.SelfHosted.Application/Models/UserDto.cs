using Knowz.Core.Enums;

namespace Knowz.SelfHosted.Application.Models;

/// <summary>
/// User data transfer object returned by auth and admin endpoints.
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public UserRole Role { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public bool IsActive { get; set; }
    public string? ApiKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
