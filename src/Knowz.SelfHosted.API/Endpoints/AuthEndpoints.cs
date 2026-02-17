using System.Security.Claims;
using Knowz.SelfHosted.Application.Interfaces;
using Knowz.SelfHosted.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace Knowz.SelfHosted.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Authentication");

        group.MapPost("/login", async (IAuthService authService, LoginRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { error = "Username and password are required." });

            try
            {
                var result = await authService.LoginAsync(request.Username, request.Password);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Json(new { error = "Invalid username or password." }, statusCode: 401);
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth")
        .Produces<AuthResult>()
        .Produces(401)
        .Produces(400)
        .Produces(429);

        group.MapGet("/me", async (HttpContext context, IAuthService authService) =>
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
                           ?? context.User.FindFirst("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Json(new { error = "Not authenticated." }, statusCode: 401);
            }

            var user = await authService.GetCurrentUserAsync(userId);
            if (user is null)
                return Results.Json(new { error = "User not found." }, statusCode: 401);

            return Results.Ok(user);
        })
        .Produces<UserDto>()
        .Produces(401);

        // --- SSO Endpoints ---

        group.MapGet("/sso/providers", async (ISelfHostedSSOService ssoService) =>
        {
            var providers = await ssoService.GetEnabledProvidersAsync();
            return Results.Ok(new { success = true, data = providers });
        })
        .AllowAnonymous();

        group.MapGet("/sso/authorize", async (
            [FromQuery] string provider,
            [FromQuery] string? redirectUri,
            ISelfHostedSSOService ssoService) =>
        {
            var effectiveRedirectUri = redirectUri ?? "/auth/sso/callback";

            var result = await ssoService.GenerateAuthorizeUrlAsync(provider, effectiveRedirectUri);
            if (!result.Success)
                return Results.BadRequest(new { error = result.ErrorMessage });

            return Results.Ok(new { success = true, data = new
            {
                authorizationUrl = result.AuthorizationUrl,
                state = result.State,
            }});
        })
        .AllowAnonymous();

        group.MapPost("/sso/callback", async (SSOCallbackRequest request, ISelfHostedSSOService ssoService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.State))
                return Results.BadRequest(new { error = "Code and state are required" });

            var result = await ssoService.HandleCallbackAsync(request.Code, request.State);
            if (!result.Success)
                return Results.Json(new { error = result.ErrorMessage }, statusCode: 401);

            return Results.Ok(new { success = true, data = result });
        })
        .AllowAnonymous();
    }
}
