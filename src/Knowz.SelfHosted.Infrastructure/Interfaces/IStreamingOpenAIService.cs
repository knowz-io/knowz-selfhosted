using Knowz.Core.Models;

namespace Knowz.SelfHosted.Infrastructure.Interfaces;

/// <summary>
/// Extension of IOpenAIService that adds streaming support.
/// Defined in the selfhosted layer since the Knowz.Core NuGet package
/// does not include streaming methods.
/// </summary>
public interface IStreamingOpenAIService
{
    IAsyncEnumerable<string> AnswerQuestionStreamingAsync(
        string question,
        List<SearchResultItem> searchResults,
        string? vaultSystemPrompt = null,
        bool researchMode = false,
        CancellationToken cancellationToken = default);
}
