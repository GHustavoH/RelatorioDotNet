using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hub.Tests.Api;

//Política de versão: leitura, padrão, não suportada, isolamento de v1/v2 e OpenAPI por versão.
public class VersioningTests(HubFactory factory) : IClassFixture<HubFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ---------- v1 (isolada) ----------

    [Fact]
    public async Task V1_ExplicitHeader_ReturnsFrozenContract()
    {
        var id = await TestApi.CreateOrderAsync(_client, "Ana", 99.90m);

        var response = await _client.SendAsync(TestApi.Get($"/api/orders/{id}", "1"));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Number, json.GetProperty("total").ValueKind);
        Assert.Equal(99.90m, json.GetProperty("total").GetDecimal());
        Assert.True(new[] { "id", "customerName", "total", "status", "createdAt" }
            .ToHashSet().SetEquals(TestApi.PropertyNames(json)));
    }

    [Fact]
    public async Task V1_ViaQueryString_Works()
    {
        var id = await TestApi.CreateOrderAsync(_client);

        var response = await _client.GetAsync($"/api/orders/{id}?api-version=1");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Number, json.GetProperty("total").ValueKind);
    }

    [Fact]
    public async Task MissingVersion_UsesDefaultV1()
    {
        var id = await TestApi.CreateOrderAsync(_client);

        var response = await _client.GetAsync($"/api/orders/{id}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Number, json.GetProperty("total").ValueKind);
    }

    // ---------- v2 (isolada) ----------

    [Fact]
    public async Task V2_Header_ReturnsMoneyObject()
    {
        var id = await TestApi.CreateOrderAsync(_client, "Bia", 10.5m, "USD");

        var response = await _client.SendAsync(TestApi.Get($"/api/orders/{id}", "2"));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Object, json.GetProperty("total").ValueKind);
        Assert.Equal(10.5m, json.GetProperty("total").GetProperty("amount").GetDecimal());
        Assert.Equal("USD", json.GetProperty("total").GetProperty("currency").GetString());
        Assert.True(new[] { "id", "customerName", "total", "status", "createdAt" }
            .ToHashSet().SetEquals(TestApi.PropertyNames(json)));
    }

    [Fact]
    public async Task V2_ViaQueryString_Works()
    {
        var id = await TestApi.CreateOrderAsync(_client);

        var response = await _client.GetAsync($"/api/orders/{id}?api-version=2");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Object, json.GetProperty("total").ValueKind);
    }

    [Fact]
    public async Task HeaderAndQueryWithSameValue_Works()
    {
        var id = await TestApi.CreateOrderAsync(_client);

        var response = await _client.SendAsync(TestApi.Get($"/api/orders/{id}?api-version=2", "2"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- versão ausente / não suportada / inválida: política explícita ----------

    [Theory]
    [InlineData("3")]
    [InlineData("0")]
    [InlineData("99")]
    public async Task UnsupportedVersion_Returns400ProblemWithSupportedVersions(string version)
    {
        var id = await TestApi.CreateOrderAsync(_client);

        var response = await _client.SendAsync(TestApi.Get($"/api/orders/{id}", version));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Versão de API não suportada", json.GetProperty("title").GetString());
        Assert.Equal(new[] { 1, 2 }, json.GetProperty("supportedVersions").EnumerateArray().Select(e => e.GetInt32()).ToArray());
        Assert.True(response.Headers.Contains("api-supported-versions"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.0")]
    [InlineData("-1")]
    [InlineData("v2")]
    public async Task MalformedVersion_Returns400(string version)
    {
        var id = await TestApi.CreateOrderAsync(_client);

        var response = await _client.SendAsync(TestApi.Get($"/api/orders/{id}", version));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Versão de API inválida", json.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ConflictingHeaderAndQuery_Returns400()
    {
        var id = await TestApi.CreateOrderAsync(_client);

        var response = await _client.SendAsync(TestApi.Get($"/api/orders/{id}?api-version=2", "1"));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Versão de API ambígua", json.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    public async Task UnknownOrder_Returns404InBothVersions(string version)
    {
        var response = await _client.SendAsync(TestApi.Get($"/api/orders/{Guid.NewGuid()}", version));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidBody_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new { customerName = "", amount = 0, currency = "BR" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ProcessesOrderAndNotifies_ThroughModules()
    {
        var id = await TestApi.CreateOrderAsync(_client);

        var json = await (await _client.GetAsync($"/api/orders/{id}")).Content.ReadFromJsonAsync<JsonElement>();

        // Pedidos - evento - Processamento (muda estado) - Notificações
        Assert.Equal("Processed", json.GetProperty("status").GetString());
    }

    // ---------- ciclo de vida: depreciação ----------

    [Fact]
    public async Task V1_Response_CarriesDeprecationHeaders()
    {
        var id = await TestApi.CreateOrderAsync(_client);

        var response = await _client.SendAsync(TestApi.Get($"/api/orders/{id}", "1"));

        Assert.True(response.Headers.Contains("Deprecation"));
        Assert.True(response.Headers.Contains("Sunset"));
        Assert.Contains("rel=\"deprecation\"", string.Join(",", response.Headers.GetValues("Link")));
        Assert.StartsWith("@", response.Headers.GetValues("Deprecation").Single());
    }

    [Fact]
    public async Task V2_Response_HasNoDeprecationHeaders()
    {
        var id = await TestApi.CreateOrderAsync(_client);

        var response = await _client.SendAsync(TestApi.Get($"/api/orders/{id}", "2"));

        Assert.False(response.Headers.Contains("Deprecation"));
        Assert.False(response.Headers.Contains("Sunset"));
    }

    // ---------- OpenAPI separável por versão ----------

    [Fact]
    public async Task OpenApi_V1Document_ContainsOnlyV1Contract()
    {
        var doc = await _client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("OrderV1Response", doc);
        Assert.DoesNotContain("OrderV2Response", doc);
        Assert.DoesNotContain("MoneyV2", doc);
        Assert.Contains("/api/orders/{id}", doc);
    }

    [Fact]
    public async Task OpenApi_V2Document_ContainsOnlyV2Contract()
    {
        var doc = await _client.GetStringAsync("/openapi/v2.json");

        Assert.Contains("OrderV2Response", doc);
        Assert.Contains("MoneyV2", doc);
        Assert.DoesNotContain("OrderV1Response", doc);
        Assert.Contains("/api/orders/{id}", doc);
    }
}
