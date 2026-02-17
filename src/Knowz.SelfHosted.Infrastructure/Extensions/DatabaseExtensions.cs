using Knowz.Core.Interfaces;
using Knowz.SelfHosted.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Knowz.SelfHosted.Infrastructure.Extensions;

public static class DatabaseExtensions
{
    /// <summary>
    /// Registers SelfHostedDbContext with tenant-aware scoped resolution.
    /// Uses AddDbContextFactory to register DbContextOptions, then overrides the scoped
    /// registration to create contexts using ITenantProvider for per-request tenant isolation.
    /// </summary>
    public static IServiceCollection AddSelfHostedDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("McpDb")
            ?? throw new InvalidOperationException("ConnectionStrings:McpDb is required for self-hosted mode");

        // Register DbContextOptions<SelfHostedDbContext> as singleton via factory
        services.AddDbContextFactory<SelfHostedDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.CommandTimeout(30);
                sql.EnableRetryOnFailure(3);
            });
            // Suppress PendingModelChangesWarning so MigrateAsync() can apply existing
            // migrations even when the EF model has evolved beyond the last migration.
            options.ConfigureWarnings(w =>
                w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        // Scoped registration: construct DbContext directly with ITenantProvider
        // so each request gets the correct tenant from JWT/header/fallback.
        services.AddScoped<SelfHostedDbContext>(sp =>
        {
            var dbOptions = sp.GetRequiredService<DbContextOptions<SelfHostedDbContext>>();
            var tenantProvider = sp.GetRequiredService<ITenantProvider>();
            return new SelfHostedDbContext(dbOptions, tenantProvider);
        });

        return services;
    }
}
