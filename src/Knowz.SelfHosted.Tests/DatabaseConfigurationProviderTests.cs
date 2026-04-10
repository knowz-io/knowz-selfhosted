using Knowz.SelfHosted.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;

namespace Knowz.SelfHosted.Tests;

public class DatabaseConfigurationProviderTests
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public DatabaseConfigurationProviderTests()
    {
        _dataProtectionProvider = new EphemeralDataProtectionProvider();
    }

    [Fact]
    public void Load_ReturnsEmptyData_WhenConnectionStringEmpty()
    {
        var source = new DatabaseConfigurationSource
        {
            ConnectionString = "",
            DataProtectionProvider = _dataProtectionProvider
        };
        var provider = new DatabaseConfigurationProvider(source);

        provider.Load();

        Assert.False(provider.TryGet("AnyKey", out _));
    }

    [Fact]
    public void Load_ReturnsEmptyData_WhenConnectionStringNull()
    {
        var source = new DatabaseConfigurationSource
        {
            ConnectionString = null!,
            DataProtectionProvider = _dataProtectionProvider
        };
        var provider = new DatabaseConfigurationProvider(source);

        provider.Load();

        Assert.False(provider.TryGet("AnyKey", out _));
    }

    [Fact]
    public void Load_ReturnsEmptyData_WhenConnectionFails()
    {
        var source = new DatabaseConfigurationSource
        {
            ConnectionString = "Server=nonexistent-server-12345;Database=test;Trusted_Connection=True;Connect Timeout=1;",
            DataProtectionProvider = _dataProtectionProvider
        };
        var provider = new DatabaseConfigurationProvider(source);

        // Should not throw
        provider.Load();

        Assert.False(provider.TryGet("AnyKey", out _));
    }

    [Fact]
    public void Source_Build_ReturnsProvider()
    {
        var source = new DatabaseConfigurationSource
        {
            ConnectionString = "test",
            DataProtectionProvider = _dataProtectionProvider
        };

        var configBuilder = new Microsoft.Extensions.Configuration.ConfigurationBuilder();
        var provider = source.Build(configBuilder);

        Assert.IsType<DatabaseConfigurationProvider>(provider);
    }

    [Fact]
    public void Reload_DoesNotThrow_WhenConnectionFails()
    {
        var source = new DatabaseConfigurationSource
        {
            ConnectionString = "Server=nonexistent-server-12345;Database=test;Trusted_Connection=True;Connect Timeout=1;",
            DataProtectionProvider = _dataProtectionProvider
        };
        var provider = new DatabaseConfigurationProvider(source);

        // Should not throw
        var ex = Record.Exception(() => provider.Reload());
        Assert.Null(ex);
    }

    [Fact]
    public void Load_ReturnsEmptyData_WhenNoDataProtectionProvider()
    {
        var source = new DatabaseConfigurationSource
        {
            ConnectionString = "",
            DataProtectionProvider = null
        };
        var provider = new DatabaseConfigurationProvider(source);

        provider.Load();

        Assert.False(provider.TryGet("AnyKey", out _));
    }
}
