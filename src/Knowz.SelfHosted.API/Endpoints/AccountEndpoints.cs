using System.Security.Claims;
using Knowz.SelfHosted.Application.Interfaces;

namespace Knowz.SelfHosted.API.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/account").WithTags("Account");

        group.MapPut("/profile", async (
            HttpContext context, IUserManagementService svc, UpdateProfileRequest req) =>
        {
            var userId = GetUserId(context);
            if (userId is null)
                return Results.Json(new { error = "Unauthorized." }, statusCode: 401);

            if (req.DisplayName is null && req.Email is null)
                return Results.BadRequest(new { error = "At least one field (displayName or email) is required." });

            try
            {
                var result = await svc.UpdateUserAsync(userId.Value,
                    new Knowz.SelfHosted.Application.Models.UpdateUserRequest
                    {
                        DisplayName = req.DisplayName,
                        Email = req.Email
                    });
                return Results.Ok(new { message = "Profile updated successfully.", user = result });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "User not found." });
            }
        }).Produces(200).Produces(400).Produces(401).Produces(404);

        group.MapPost("/change-password", async (
            HttpContext context, IAuthService authSvc, IUserManagementService userSvc,
            ChangePasswordRequest req) =>
        {
            var userId = GetUserId(context);
            if (userId is null)
                return Results.Json(new { error = "Unauthorized." }, statusCode: 401);

            if (string.IsNullOrWhiteSpace(req.CurrentPassword))
                return Results.BadRequest(new { error = "currentPassword is required." });
            if (string.IsNullOrWhiteSpace(req.NewPassword))
                return Results.BadRequest(new { error = "newPassword is required." });
            if (req.NewPassword.Length < 6)
                return Results.BadRequest(new { error = "New password must be at least 6 characters." });

            var user = await authSvc.GetCurrentUserAsync(userId.Value);
            if (user is null)
                return Results.NotFound(new { error = "User not found." });

            // Verify current password by attempting login
            try
            {
                var loginResult = await authSvc.LoginAsync(user.Username, req.CurrentPassword);
            }
            catch
            {
                return Results.BadRequest(new { error = "Current password is incorrect." });
            }

            try
            {
                var message = await userSvc.ResetPasswordAsync(userId.Value, req.NewPassword);
                return Results.Ok(new { message = "Password changed successfully." });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "User not found." });
            }
        }).Produces(200).Produces(400).Produces(401).Produces(404);
    }

    private static Guid? GetUserId(HttpContext context)
    {
        var claim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public record UpdateProfileRequest(string? DisplayName = null, string? Email = null);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
