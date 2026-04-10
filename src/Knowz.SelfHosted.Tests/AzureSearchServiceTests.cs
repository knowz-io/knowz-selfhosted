using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Knowz.Core.Configuration;
using Knowz.Core.Interfaces;
using Knowz.SelfHosted.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Knowz.SelfHosted.Tests;

public class AzureSearchServiceTests : IDisposable
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public void Dispose()
    {
        AzureSearchService.ResetIndexEnsuredFlag();
    }

    // ===== BuildIndexFields Tests =====

    [Fact]
    public void BuildIndexFields_Returns19Fields()
    {
        var fields = AzureSearchService.BuildIndexFields();
        Assert.Equal(19, fields.Count);
    }

    [Fact]
    public void BuildIndexFields_ContainsAllExpectedFieldNames()
    {
        var fields = AzureSearchService.BuildIndexFields();
        var fieldNames = fields.Select(f => f.Name).ToHashSet();

        var expected = new[]
        {
            "id", "tenantId", "knowledgeId", "title", "content", "aiSummary",
            "vaultName", "vaultId", "vaultIds", "ancestorVaultIds",
            "topicName", "tags", "knowledgeTypeId", "filePath", "contentVector",
            "createdAt", "updatedAt", "documentType", "position"
        };

        foreach (var name in expected)
        {
            Assert.Contains(name, fieldNames);
        }
    }

    [Fact]
    public void BuildIndexFields_IdFieldIsKey()
    {
        var fields = AzureSearchService.BuildIndexFields();
        var idField = fields.First(f => f.Name == "id");

        Assert.True(idField.IsKey);
        Assert.True(idField.IsFilterable);
    }

    [Fact]
    public void BuildIndexFields_NewFields_HaveCorrectTypes()
    {
        var fields = AzureSearchService.BuildIndexFields();

        var updatedAt = fields.First(f => f.Name == "updatedAt");
        Assert.Equal(SearchFieldDataType.DateTimeOffset, updatedAt.Type);
        Assert.True(updatedAt.IsFilterable);
        Assert.True(updatedAt.IsSortable);

        var documentType = fields.First(f => f.Name == "documentType");
        Assert.Equal(SearchFieldDataType.String, documentType.Type);
        Assert.True(documentType.IsFilterable);

        var position = fields.First(f => f.Name == "position");
        Assert.Equal(SearchFieldDataType.Int32, position.Type);
        Assert.True(position.IsFilterable);
        Assert.True(position.IsSortable);
    }

    [Fact]
    public void BuildIndexFields_TagsAreSearchable()
    {
        var fields = AzureSearchService.BuildIndexFields();
        var tags = fields.First(f => f.Name == "tags");

        Assert.True(tags.IsSearchable);
        Assert.True(tags.IsFilterable);
    }

    [Fact]
    public void BuildIndexFields_TopicNameIsSearchableAndFilterable()
    {
        var fields = AzureSearchService.BuildIndexFields();
        var topicName = fields.First(f => f.Name == "topicName");

        Assert.True(topicName.IsSearchable);
        Assert.True(topicName.IsFilterable);
    }

    [Fact]
    public void BuildIndexFields_ContentVectorHasCorrectConfig()
    {
        var fields = AzureSearchService.BuildIndexFields();
        var vector = fields.First(f => f.Name == "contentVector");

        Assert.True(vector.IsSearchable);
        Assert.Equal(1536, vector.VectorSearchDimensions);
        Assert.Equal("default-vector-profile", vector.VectorSearchProfileName);
    }

    [Fact]
    public void BuildIndexFields_VaultNameIsFilterable()
    {
        var fields = AzureSearchService.BuildIndexFields();
        var vaultName = fields.First(f => f.Name == "vaultName");

        Assert.True(vaultName.IsFilterable);
    }

    // ===== BuildDocumentId Tests =====

    [Fact]
    public void BuildDocumentId_WithoutChunk_ReturnsTenantAndKnowledgeId()
    {
        var svc = CreateService();
        var knowledgeId = Guid.NewGuid();

        var result = svc.BuildDocumentId(knowledgeId);

        Assert.Equal($"{TenantId}_{knowledgeId}", result);
    }

    [Fact]
    public void BuildDocumentId_WithChunk_IncludesChunkIndex()
    {
        var svc = CreateService();
        var knowledgeId = Guid.NewGuid();

        var result = svc.BuildDocumentId(knowledgeId, 3);

        Assert.Equal($"{TenantId}_{knowledgeId}_chunk_3", result);
    }

    // ===== BuildFilter Tests =====

    [Fact]
    public void BuildFilter_NoFilters_ReturnsTenantFilter()
    {
        var svc = CreateService();

        var filter = svc.BuildFilter(null, true, null, false, null, null);

        Assert.Equal($"tenantId eq '{TenantId}'", filter);
    }

    [Fact]
    public void BuildFilter_WithVaultIdAndDescendants_IncludesAncestorFilter()
    {
        var svc = CreateService();
        var vaultId = Guid.NewGuid();

        var filter = svc.BuildFilter(vaultId, includeDescendants: true, null, false, null, null);

        Assert.Contains("ancestorVaultIds/any", filter);
        Assert.Contains(vaultId.ToString(), filter);
    }

    [Fact]
    public void BuildFilter_WithVaultIdNoDescendants_ExactMatch()
    {
        var svc = CreateService();
        var vaultId = Guid.NewGuid();

        var filter = svc.BuildFilter(vaultId, includeDescendants: false, null, false, null, null);

        Assert.Contains($"vaultId eq '{vaultId}'", filter);
        Assert.DoesNotContain("ancestorVaultIds", filter);
    }

    [Fact]
    public void BuildFilter_WithTags_RequireAll_UsesAnd()
    {
        var svc = CreateService();
        var tags = new[] { "meeting", "notes" };

        var filter = svc.BuildFilter(null, true, tags, requireAllTags: true, null, null);

        Assert.Contains("tags/any(t: t eq 'meeting')", filter);
        Assert.Contains("tags/any(t: t eq 'notes')", filter);
        // Both tag filters are joined with "and"
        Assert.DoesNotContain(" or tags/any", filter);
    }

    [Fact]
    public void BuildFilter_WithTags_AnyMatch_UsesOr()
    {
        var svc = CreateService();
        var tags = new[] { "meeting", "notes" };

        var filter = svc.BuildFilter(null, true, tags, requireAllTags: false, null, null);

        Assert.Contains(" or ", filter);
    }

    [Fact]
    public void BuildFilter_WithDateRange_IncludesDateFilters()
    {
        var svc = CreateService();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var filter = svc.BuildFilter(null, true, null, false, start, end);

        Assert.Contains("createdAt ge 2026-01-01T00:00:00Z", filter);
        Assert.Contains("createdAt le 2026-12-31T23:59:59Z", filter);
    }

    // ===== EscapeOData Tests =====

    [Fact]
    public void EscapeOData_EscapesSingleQuotes()
    {
        Assert.Equal("it''s a test", AzureSearchService.EscapeOData("it's a test"));
    }

    [Fact]
    public void EscapeOData_NoQuotes_Unchanged()
    {
        Assert.Equal("simple", AzureSearchService.EscapeOData("simple"));
    }

    // ===== Semantic Configuration Tests =====

    [Fact]
    public void BuildSemanticConfiguration_ReturnsConfigNamedSelfhostedSemantic()
    {
        var config = AzureSearchService.BuildSemanticConfiguration();

        Assert.Equal("selfhosted-semantic", config.Name);
    }

    [Fact]
    public void BuildSemanticConfiguration_TitleFieldIsTitle()
    {
        var config = AzureSearchService.BuildSemanticConfiguration();

        Assert.Equal("title", config.PrioritizedFields.TitleField.FieldName);
    }

    [Fact]
    public void BuildSemanticConfiguration_ContentFieldsContainContentAndAiSummary()
    {
        var config = AzureSearchService.BuildSemanticConfiguration();

        var contentFieldNames = config.PrioritizedFields.ContentFields
            .Select(f => f.FieldName).ToList();

        Assert.Contains("content", contentFieldNames);
        Assert.Contains("aiSummary", contentFieldNames);
    }

    [Fact]
    public void BuildSemanticConfiguration_KeywordsFieldsContainTags()
    {
        var config = AzureSearchService.BuildSemanticConfiguration();

        var keywordFieldNames = config.PrioritizedFields.KeywordsFields
            .Select(f => f.FieldName).ToList();

        Assert.Contains("tags", keywordFieldNames);
    }

    // ===== Helper =====

    private static AzureSearchService CreateService()
    {
        var searchClient = Substitute.For<SearchClient>();
        // SearchClient.IndexName getter needs to be configured
        searchClient.IndexName.Returns("test-index");

        var indexClient = Substitute.For<SearchIndexClient>();
        var logger = Substitute.For<ILogger<AzureSearchService>>();
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(TenantId);

        return new AzureSearchService(searchClient, indexClient, logger, tenantProvider);
    }
}
