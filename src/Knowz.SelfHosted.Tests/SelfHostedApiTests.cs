using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Knowz.SelfHosted.Tests;

/// <summary>
/// Integration tests for the Self-Hosted API running against real Azure resources.
/// Requires the API to be running locally on port 5000 with valid configuration.
/// Run with: dotnet test src/Knowz.SelfHosted.Tests --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class SelfHostedApiTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly string _apiKey = "test-api-key";
    private readonly string _baseUrl = "http://localhost:5000";

    // Track created resources for cleanup
    private readonly List<Guid> _createdVaultIds = new();
    private readonly List<Guid> _createdKnowledgeIds = new();

    public SelfHostedApiTests()
    {
        _client = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        _client.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Cleanup created knowledge items first (may have FK to vaults)
        foreach (var id in _createdKnowledgeIds)
        {
            try { await _client.DeleteAsync($"/api/v1/knowledge/{id}"); }
            catch { /* best effort */ }
        }

        // Cleanup created vaults
        foreach (var vaultId in _createdVaultIds)
        {
            try { await _client.DeleteAsync($"/api/v1/vaults/{vaultId}"); }
            catch { /* best effort */ }
        }

        _client.Dispose();
    }

    // --- Health ---

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/healthz");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", body.GetProperty("status").GetString());
        Assert.Equal("1.0.0", body.GetProperty("version").GetString());
    }

    // --- Auth ---

    [Fact]
    public async Task NoApiKey_Returns401()
    {
        using var noAuthClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        var response = await noAuthClient.GetAsync("/api/v1/vaults");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongApiKey_Returns401()
    {
        using var badClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        badClient.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");
        var response = await badClient.GetAsync("/api/v1/vaults");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DifferentLengthApiKey_Returns401NotException()
    {
        // Test that keys of different lengths return 401, not 500 from FixedTimeEquals exception
        // Create separate client to avoid interfering with other tests that use _client
        var testApiKeys = new[] { "short", "this-is-a-very-long-api-key-that-is-much-longer-than-the-configured-one" };
        
        foreach (var testKey in testApiKeys)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/v1/vaults");
            request.Headers.Add("X-Api-Key", testKey);
            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task HealthEndpoint_SkipsAuth()
    {
        using var noAuthClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        var response = await noAuthClient.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SpaRoutes_AllowGetRequestsWithoutAuth()
    {
        using var noAuthClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        
        // Test various SPA routes that should serve index.html without auth
        var spaRoutes = new[] { "/", "/vaults", "/knowledge", "/search", "/settings" };
        
        foreach (var route in spaRoutes)
        {
            var response = await noAuthClient.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            var contentType = response.Content.Headers.ContentType?.MediaType;
            Assert.Equal("text/html", contentType);
        }
    }

    // --- Vaults ---

    [Fact]
    public async Task Vaults_ListReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/vaults");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("vaults", out _));
    }

    [Fact]
    public async Task Vaults_CreateAndList()
    {
        var vaultName = $"TestVault-{Guid.NewGuid():N}";

        // Create
        var createResponse = await _client.PostAsJsonAsync("/api/v1/vaults", new
        {
            name = vaultName,
            description = "Integration test vault"
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var vaultId = Guid.Parse(created.GetProperty("id").GetString()!);
        _createdVaultIds.Add(vaultId);

        Assert.Equal(vaultName, created.GetProperty("name").GetString());
        Assert.True(created.GetProperty("created").GetBoolean());

        // Verify in list
        var listResponse = await _client.GetAsync("/api/v1/vaults");
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var vaults = list.GetProperty("vaults");

        Assert.Contains(vaults.EnumerateArray().ToList(),
            v => v.GetProperty("id").GetString() == vaultId.ToString());
    }

    [Fact]
    public async Task Vaults_CreateRequiresName()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/vaults", new
        {
            name = "",
            description = "Missing name"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Knowledge ---

    [Fact]
    public async Task Knowledge_CreateAndRetrieve()
    {
        var title = $"Test-{Guid.NewGuid():N}";

        // Create
        var createResponse = await _client.PostAsJsonAsync("/api/v1/knowledge", new
        {
            title,
            content = "This is test content for integration testing.",
            type = "Note",
            tags = new[] { "test", "integration" }
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var knowledgeId = Guid.Parse(created.GetProperty("id").GetString()!);
        _createdKnowledgeIds.Add(knowledgeId);

        Assert.Equal(title, created.GetProperty("title").GetString());

        // Retrieve
        var getResponse = await _client.GetAsync($"/api/v1/knowledge/{knowledgeId}");
        getResponse.EnsureSuccessStatusCode();

        var item = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(title, item.GetProperty("title").GetString());
        Assert.Equal("This is test content for integration testing.", item.GetProperty("content").GetString());
        Assert.Equal("Note", item.GetProperty("type").GetString());

        // Check tags
        var tags = item.GetProperty("tags").EnumerateArray()
            .Select(t => t.GetString())
            .ToList();
        Assert.Contains("test", tags);
        Assert.Contains("integration", tags);
    }

    [Fact]
    public async Task Knowledge_CreateInVault()
    {
        // Create vault first
        var vaultName = $"VaultForKnowledge-{Guid.NewGuid():N}";
        var vaultResp = await _client.PostAsJsonAsync("/api/v1/vaults", new
        {
            name = vaultName,
            description = "Vault for knowledge test"
        });
        var vaultBody = await vaultResp.Content.ReadFromJsonAsync<JsonElement>();
        var vaultId = vaultBody.GetProperty("id").GetString()!;
        _createdVaultIds.Add(Guid.Parse(vaultId));

        // Create knowledge in vault
        var createResponse = await _client.PostAsJsonAsync("/api/v1/knowledge", new
        {
            title = "Knowledge in vault",
            content = "Content linked to a specific vault.",
            type = "Note",
            vaultId
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var knowledgeId = Guid.Parse(created.GetProperty("id").GetString()!);
        _createdKnowledgeIds.Add(knowledgeId);

        // Verify vault association
        var getResp = await _client.GetAsync($"/api/v1/knowledge/{knowledgeId}");
        var item = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        var vaults = item.GetProperty("vaults").EnumerateArray().ToList();
        Assert.Single(vaults);
        Assert.Equal(vaultId, vaults[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Knowledge_ListWithPagination()
    {
        var response = await _client.GetAsync("/api/v1/knowledge?page=1&pageSize=5");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(5, body.GetProperty("pageSize").GetInt32());
        Assert.True(body.GetProperty("totalItems").GetInt32() >= 0);
    }

    [Fact]
    public async Task Knowledge_UpdateItem()
    {
        // Create
        var createResp = await _client.PostAsJsonAsync("/api/v1/knowledge", new
        {
            title = "Original Title",
            content = "Original content.",
            type = "Note"
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(created.GetProperty("id").GetString()!);
        _createdKnowledgeIds.Add(id);

        // Update
        var updateResp = await _client.PutAsJsonAsync($"/api/v1/knowledge/{id}", new
        {
            title = "Updated Title",
            content = "Updated content."
        });
        updateResp.EnsureSuccessStatusCode();

        // Verify
        var getResp = await _client.GetAsync($"/api/v1/knowledge/{id}");
        var item = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Title", item.GetProperty("title").GetString());
        Assert.Equal("Updated content.", item.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Knowledge_DeleteItem()
    {
        // Create
        var createResp = await _client.PostAsJsonAsync("/api/v1/knowledge", new
        {
            title = "To be deleted",
            content = "This will be soft-deleted.",
            type = "Note"
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString()!;

        // Delete
        var deleteResp = await _client.DeleteAsync($"/api/v1/knowledge/{id}");
        deleteResp.EnsureSuccessStatusCode();

        var deleted = await deleteResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(deleted.GetProperty("deleted").GetBoolean());
    }

    [Fact]
    public async Task Knowledge_RequiresContent()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/knowledge", new
        {
            title = "No content",
            content = "",
            type = "Note"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Stats ---

    [Fact]
    public async Task Knowledge_StatsReturnsValidShape()
    {
        var response = await _client.GetAsync("/api/v1/knowledge/stats");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("totalKnowledgeItems", out _));
        Assert.True(body.TryGetProperty("byType", out _));
        Assert.True(body.TryGetProperty("byVault", out _));
    }

    // --- Search ---

    [Fact]
    public async Task Search_ReturnsValidShape()
    {
        var response = await _client.GetAsync("/api/v1/search?q=test");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out _));
        Assert.True(body.TryGetProperty("totalResults", out _));
    }

    // --- Vault Contents ---

    [Fact]
    public async Task Vaults_ContentsEndpoint()
    {
        // Create vault
        var vaultResp = await _client.PostAsJsonAsync("/api/v1/vaults", new
        {
            name = $"ContentsTest-{Guid.NewGuid():N}",
            description = "Test vault contents"
        });
        var vaultBody = await vaultResp.Content.ReadFromJsonAsync<JsonElement>();
        var vaultId = vaultBody.GetProperty("id").GetString()!;
        _createdVaultIds.Add(Guid.Parse(vaultId));

        // Create knowledge in vault
        var knowledgeResp = await _client.PostAsJsonAsync("/api/v1/knowledge", new
        {
            title = "Vault content item",
            content = "Content inside vault.",
            type = "Note",
            vaultId
        });
        var knowledgeBody = await knowledgeResp.Content.ReadFromJsonAsync<JsonElement>();
        _createdKnowledgeIds.Add(Guid.Parse(knowledgeBody.GetProperty("id").GetString()!));

        // Get vault contents
        var contentsResp = await _client.GetAsync($"/api/v1/vaults/{vaultId}/contents");
        contentsResp.EnsureSuccessStatusCode();

        var contents = await contentsResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(vaultId, contents.GetProperty("vaultId").GetString());
        Assert.True(contents.GetProperty("totalItems").GetInt32() >= 1);
    }

    // --- Inbox ---

    [Fact]
    public async Task Inbox_CreateItem()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/inbox", new
        {
            body = "This is a test inbox item for integration testing."
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task Inbox_RequiresBody()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/inbox", new
        {
            body = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Swagger ---

    [Fact]
    public async Task Swagger_IsAccessible()
    {
        using var noAuthClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        var response = await noAuthClient.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Knowz Self-Hosted API", body.GetProperty("info").GetProperty("title").GetString());
    }
}
