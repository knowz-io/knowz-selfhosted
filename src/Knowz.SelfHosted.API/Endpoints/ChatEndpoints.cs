using Knowz.SelfHosted.API.Models;
using Knowz.SelfHosted.Application.DTOs;
using Knowz.SelfHosted.Application.Interfaces;
using Knowz.SelfHosted.Application.Services;

namespace Knowz.SelfHosted.API.Endpoints;

public static class ChatEndpoints
{
    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase) { "user", "assistant" };
    private static readonly System.Text.Json.JsonSerializerOptions CamelCaseJson = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    public static void MapChatEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/chat", async (
            SearchFacade svc,
            IVaultAccessService vaultAccessService,
            HttpContext context,
            ChatRequest req,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Question))
                return Results.BadRequest(new { error = "question is required" });

            // Validate conversation history roles
            if (req.ConversationHistory != null)
            {
                foreach (var msg in req.ConversationHistory)
                {
                    if (!ValidRoles.Contains(msg.Role))
                        return Results.BadRequest(new { error = $"Invalid role '{msg.Role}'. Must be 'user' or 'assistant'." });
                }
            }

            var accessibleVaultIds = await VaultEndpoints.ResolveAccessibleVaultIdsAsync(context, vaultAccessService, ct);

            Guid? vaultId = null;
            if (!string.IsNullOrWhiteSpace(req.VaultId) && Guid.TryParse(req.VaultId, out var vid))
                vaultId = vid;

            var maxTurns = Math.Clamp(req.MaxTurns, 1, 50);

            var history = req.ConversationHistory?
                .Select(m => new ChatMessageDto(m.Role, m.Content))
                .ToList();

            var result = await svc.ChatWithHistoryAsync(
                req.Question, history, vaultId, req.ResearchMode, maxTurns, ct, accessibleVaultIds);

            return Results.Ok(result);
        }).WithTags("Chat").Produces<ChatResponse>(200).Produces(400);

        app.MapPost("/api/v1/chat/stream", async (
            SearchFacade svc,
            IVaultAccessService vaultAccessService,
            HttpContext context,
            ChatRequest req,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Question))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "question is required" });
                return;
            }

            if (req.ConversationHistory != null)
            {
                foreach (var msg in req.ConversationHistory)
                {
                    if (!ValidRoles.Contains(msg.Role))
                    {
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsJsonAsync(new { error = $"Invalid role '{msg.Role}'. Must be 'user' or 'assistant'." });
                        return;
                    }
                }
            }

            var accessibleVaultIds = await VaultEndpoints.ResolveAccessibleVaultIdsAsync(context, vaultAccessService, ct);

            Guid? vaultId = null;
            if (!string.IsNullOrWhiteSpace(req.VaultId) && Guid.TryParse(req.VaultId, out var vid))
                vaultId = vid;

            var maxTurns = Math.Clamp(req.MaxTurns, 1, 50);

            var history = req.ConversationHistory?
                .Select(m => new ChatMessageDto(m.Role, m.Content))
                .ToList();

            context.Response.Headers["Content-Type"] = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";

            try
            {
                var result = await svc.ChatWithHistoryStreamingAsync(
                    req.Question, history, vaultId, req.ResearchMode, maxTurns, ct, accessibleVaultIds);

                var sourcesJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    type = "sources",
                    sources = result.Sources,
                    confidence = result.Confidence
                }, CamelCaseJson);
                await context.Response.WriteAsync($"data: {sourcesJson}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);

                await foreach (var token in result.TokenStream.WithCancellation(ct))
                {
                    var tokenJson = System.Text.Json.JsonSerializer.Serialize(new { type = "token", content = token }, CamelCaseJson);
                    await context.Response.WriteAsync($"data: {tokenJson}\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);
                }

                await context.Response.WriteAsync("data: {\"type\":\"done\"}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Client disconnected - normal, don't log as error
            }
            catch (Exception ex)
            {
                try
                {
                    var errorJson = System.Text.Json.JsonSerializer.Serialize(new { type = "error", message = ex.Message }, CamelCaseJson);
                    await context.Response.WriteAsync($"data: {errorJson}\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);
                }
                catch { /* Client may have disconnected */ }
            }
        }).WithTags("Chat");
    }
}
