using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Knowz.SelfHosted.Infrastructure.Services;

/// <summary>
/// Custom ConfigurationProvider that reads SystemConfiguration rows from the database
/// and maps them into the IConfiguration pipeline as "{Category}:{Key}" entries.
/// Decrypts EncryptedValue using Data Protection API on load.
/// </summary>
public class DatabaseConfigurationProvider : ConfigurationProvider
{
    private readonly DatabaseConfigurationSource _source;
    private static readonly string ProtectorPurpose = "Knowz.SelfHosted.SystemConfiguration";

    public DatabaseConfigurationProvider(DatabaseConfigurationSource source)
    {
        _source = source;
    }

    /// <summary>
    /// Loads all SystemConfiguration rows from the database into the Data dictionary.
    /// Called once at startup. Decrypts EncryptedValue for each row.
    /// Silently catches exceptions if DB is unavailable (logs warning via console).
    /// </summary>
    public override void Load()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(_source.ConnectionString))
        {
            Data = data;
            return;
        }

        try
        {
            using var connection = new SqlConnection(_source.ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Category, [Key], EncryptedValue FROM SystemConfigurations";

            using var reader = command.ExecuteReader();
            var protector = _source.DataProtectionProvider?.CreateProtector(ProtectorPurpose);

            while (reader.Read())
            {
                var category = reader.GetString(0);
                var key = reader.GetString(1);
                var encryptedValue = reader.IsDBNull(2) ? null : reader.GetString(2);

                if (encryptedValue is null)
                    continue;

                string? decryptedValue = null;
                if (protector is not null)
                {
                    try
                    {
                        decryptedValue = protector.Unprotect(encryptedValue);
                    }
                    catch
                    {
                        // If decryption fails (e.g., key mismatch), skip — file-based config provides defaults
                        continue;
                    }
                }
                else
                {
                    // Data Protection not yet initialized — skip encrypted DB values,
                    // fall back to file-based config (appsettings.json / environment vars)
                    continue;
                }

                var configKey = $"{category}:{key}";
                data[configKey] = decryptedValue;
            }
        }
        catch (Exception)
        {
            // DB not available (first run before migration, connection failure, etc.)
            // Fall back to file-based config silently.
        }

        Data = data;
    }

    /// <summary>
    /// Reloads configuration from the database. Called by the Service layer
    /// after an admin saves config changes. Triggers IOptionsMonitor change tokens.
    /// </summary>
    public void Reload()
    {
        Load();
        OnReload();
    }
}
