using Knowz.SelfHosted.Setup.Models;
using Knowz.SelfHosted.Setup.Services;
using Spectre.Console;

namespace Knowz.SelfHosted.Setup.Steps;

public static class CredentialsStep
{
    public static Task RunAsync(SetupConfig config)
    {
        AnsiConsole.MarkupLine("[bold]Credentials[/]");

        config.AdminUsername = AnsiConsole.Prompt(
            new TextPrompt<string>("Admin [green]username[/]:")
                .DefaultValue("admin"));

        config.AdminPassword = AnsiConsole.Prompt(
            new TextPrompt<string>("Admin [green]password[/]:")
                .DefaultValue("changeme")
                .Secret('*'));

        if (config.AdminPassword == "changeme")
            AnsiConsole.MarkupLine("[yellow]Warning: Using default password. Change this for production![/]");

        var autoJwt = AnsiConsole.Confirm("Auto-generate JWT secret? (64 random chars)", defaultValue: true);
        config.JwtSecret = autoJwt
            ? ConfigValidator.GenerateJwtSecret()
            : AnsiConsole.Prompt(
                new TextPrompt<string>("JWT [green]secret[/] (min 32 chars):")
                    .Secret('*')
                    .Validate(s => ConfigValidator.IsValidJwtSecret(s)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("JWT secret must be at least 32 characters")));

        config.SaPassword = AnsiConsole.Prompt(
            new TextPrompt<string>("SQL SA [green]password[/]:")
                .DefaultValue("Knowz_Dev_P@ssw0rd!")
                .Secret('*')
                .Validate(p => ConfigValidator.IsValidSqlPassword(p)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Must be 8+ chars with 3 of: uppercase, lowercase, digit, symbol")));

        config.McpServiceKey = AnsiConsole.Prompt(
            new TextPrompt<string>("MCP service [green]key[/]:")
                .DefaultValue("knowz-mcp-dev-service-key"));

        return Task.CompletedTask;
    }
}
