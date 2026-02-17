using Knowz.SelfHosted.API.Models;
using Knowz.SelfHosted.Application.DTOs;
using Knowz.SelfHosted.Application.Interfaces;
using Knowz.SelfHosted.Application.Services;

namespace Knowz.SelfHosted.API.Endpoints;

public static class ChatEndpoints
{
    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase) { "user", "assistant" };

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
    }
}
