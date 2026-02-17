using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace Knowz.SelfHosted.Infrastructure.Services;

/// <summary>
/// Configuration source that loads config from the SystemConfigurations database table.
/// Added to the IConfigurationBuilder pipeline so DB values override file-based config.
/// </summary>
public class DatabaseConfigurationSource : IConfigurationSource
{
    public string ConnectionString { get; set; } = string.Empty;
    public IDataProtectionProvider? DataProtectionProvider { get; set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DatabaseConfigurationProvider(this);
    }
}
