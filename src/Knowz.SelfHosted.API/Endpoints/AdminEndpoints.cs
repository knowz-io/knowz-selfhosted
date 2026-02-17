using System.Security.Claims;
using Knowz.SelfHosted.Application.Interfaces;
using Knowz.SelfHosted.Application.Models;

namespace Knowz.SelfHosted.API.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/admin").WithTags("Administration");

        // --- Tenant endpoints ---

        group.MapGet("/tenants", async (HttpContext context, ITenantManagementService svc) =>
        {
            if (!IsSuperAdmin(context)) return Results.Json(new { error = "Forbidden." }, statusCode: 403);
            return Results.Ok(await svc.ListTenantsAsync());
        }).Produces<List<TenantDto>>().Produces(403);

        group.MapGet("/tenants/{id:guid}", async (HttpContext context, ITenantManagementService svc, Guid id) =>
        {
            if (!IsSuperAdmin(context)) return Results.Json(new { error = "Forbidden." }, statusCode: 403);
            var result = await svc.GetTenantAsync(id);
            return result is null
                ? Results.NotFound(new { error = "Tenant not found." })
                : Results.Ok(result);
        }).Produces<TenantDto>().Produces(403).Produces(404);

        group.MapPost("/tenants", async (HttpContext context, ITenantManagementService svc, CreateTenantRequest request) =>
        {
            if (!IsSuperAdmin(context)) return Results.Json(new { error = "Forbidden." }, statusCode: 403);

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "Name is required." });
            if (string.IsNullOrWhiteSpace(request.Slug))
                return Results.BadRequest(new { error = "Slug is required." });

            try
            {
                var result = await svc.CreateTenantAsync(request);
                return Results.Created($"/api/v1/admin/tenants/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).Produces<TenantDto>(201).Produces(400).Produces(403).Produces(409);

        group.MapPut("/tenants/{id:guid}", async (HttpContext context, ITenantManagementService svc, Guid id, UpdateTenantRequest request) =>
        {
            if (!IsSuperAdmin(context)) return Results.Json(new { error = "Forbidden." }, statusCode: 403);

            try
            {
                var result = await svc.UpdateTenantAsync(id, request);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "Tenant not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).Produces<TenantDto>().Produces(403).Produces(404).Produces(409);

        group.MapDelete("/tenants/{id:guid}", async (HttpContext context, ITenantManagementService svc, Guid id) =>
        {
            if (!IsSuperAdmin(context)) return Results.Json(new { error = "Forbidden." }, statusCode: 403);

            try
            {
                await svc.DeleteTenantAsync(id);
                return Results.Ok(new { message = "Tenant deleted successfully." });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "Tenant not found." });
            }
        }).Produces(200).Produces(403).Produces(404);

        // --- User endpoints ---

        group.MapGet("/users", async (HttpContext context, IUserManagementService svc, Guid? tenantId) =>
        {
            if (!IsSuperAdmin(context)) return Results.Json(new { error = "Forbidden." }, statusCode: 403);
            return Results.Ok(await svc.ListUsersAsync(tenantId));
        }).Produces<List<UserDto>>().Produces(403);

        group.MapGet("/users/{id:guid}", async (HttpContext context, IUserManagementService svc, Guid id) =>
        {
            if (!IsSuperAdmin(context)) return Results.Json(new { error = "Forbidden." }, statusCode: 403);
            var result = await svc.GetUserAsync(id);
            return result is null
                ? Results.NotFound(new { error = "User not found." })
                : Results.Ok(result);
        }).Produces<UserDto>().Produces(403).Produces(404);

        group.MapPost("/users", async (HttpContext context, IUserManagementService svc, CreateUserRequest request) =>
        {
            if (!IsSuperAdmin(context)) return Results.Json(new { error = "Forbidden." }, statusCode: 403);

            if (string.IsNullOrWhiteSpace(request.Username))
                return Results.BadRequest(new { error = "Username is required." });
            if (string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { error = "Password is required." });
            if (request.Password.Length < 6)
                return Results.BadRequest(new { error = "Password must be at least 6 characters." });

            try
            {
                var result = await svc.CreateUserAsync(request);
                return Results.Created($"/api/v1/admin/users/{result.Id}", result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        }).Produces<UserDto>(201).Produces(400).Produces(403).Produces(404).Produces(409);

        group.MapPut("/users/{id:guid}", async (HttpContext context, IUserManagementService svc, Guid id, UpdateUserRequest request) =>
        {
            if (!IsSuperAdmin(context)) return Results.Json(new { error = "Forbidden." }, statusCode: 403);

            try
            {
                var result = await svc.UpdateUserAsync(id, request);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "User not found." });
            }
        }).Produces<UserDto>().Produces(403).Produces(404);

        group.MapDelete("/users/{id:guid}", async (HttpContext context, IUserManagementService svc, Guid id) =>
        {
            if (!IsSuperAdmin(context)) return Results.Json(new { error = "Forbidden." }, statusCode: 403);

            try
            {
                await svc.DeleteUserAsync(id);
                return Results.Ok(new { message = "User deleted successfully." });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "User not found." });
            }
        }).Produces(200).Produces(403).Produces(404);

        group.MapPost("/users/{id:guid}/generate-api-key", async (HttpContext context, IUserManagementService svc, Guid id) =>
        {
            if (!IsSuperAdmin(context)) return Results.Json(new { error = "Forbidden." }, statusCode: 403);

            try
            {
                var apiKey = await svc.GenerateApiKeyAsync(id);
                return Results.Ok(new { apiKey });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "User not found." });
            }
        }).Produces(200).Produces(403).Produces(404);

        group.MapPost("/users/{id:guid}/reset-password", async (HttpContext context, IUserManagementService svc, Guid id, ResetPasswordRequest request) =>
        {
            if (!IsSuperAdmin(context)) return Results.Json(new { error = "Forbidden." }, statusCode: 403);

            if (string.IsNullOrWhiteSpace(request.NewPassword))
                return Results.BadRequest(new { error = "New password is required." });
            if (request.NewPassword.Length < 6)
                return Results.BadRequest(new { error = "Password must be at least 6 characters." });

            try
            {
                var message = await svc.ResetPasswordAsync(id, request.NewPassword);
                return Results.Ok(new { message });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "User not found." });
            }
        }).Produces(200).Produces(400).Produces(403).Produces(404);
    }

    /// <summary>
    /// Checks if the current user has the SuperAdmin role.
    /// </summary>
    private static bool IsSuperAdmin(HttpContext context)
    {
        return context.User.IsInRole("SuperAdmin");
    }
}
