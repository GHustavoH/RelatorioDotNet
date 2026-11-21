using System.Net.Http.Json;
using System.Text.Json;
using Hub.Tests.Api;

namespace Hub.Tests.Compatibility;

// Executa o cliente v1 contra a MESMA aplicação que também contém v2,
// provando que a evolução não rompe o consumidor anterior.
public class V1CompatibilityTests(HubFactory factory) : IClassFixture<HubFactory>
{
    [Fact]
    public async Task LegacyClient_ReadsOrder_WithSameSemantics_WhileV2Coexists()
    {
        var http = factory.CreateClient();
        var legacy = new LegacyFrontendV1Client(http);

        var id = await legacy.PlaceOrderAsync("Carlos", 150.75m);

        // v2 ativa e usada em paralelo na mesma aplicação
        var v2Json = await (await http.SendAsync(TestApi.Get($"/api/orders/{id}", "2")))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, v2Json.GetProperty("total").ValueKind);

        //.e o cliente v1 segue funcionando sem nenhuma mudança.
        var order = await legacy.GetOrderAsync(id);

        Assert.Equal(id, order.Id);
        Assert.Equal("Carlos", order.CustomerName);
        Assert.Equal(150.75m, order.Total);          // semântica: continua sendo o valor numérico do pedido
        Assert.Equal("Processed", order.Status);
        Assert.True(order.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task V1Contract_IsFrozen_SamePropertiesAndTypes()
    {
        var http = factory.CreateClient();
        var id = await TestApi.CreateOrderAsync(http, "Dani", 42m);

        var json = await (await http.GetAsync($"/api/orders/{id}")).Content.ReadFromJsonAsync<JsonElement>();

        // Snapshot do contrato v1: nomes e tipos JSON não podem mudar silenciosamente.
        var snapshot = json.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind);

        Assert.Equal(5, snapshot.Count);
        Assert.Equal(JsonValueKind.String, snapshot["id"]);
        Assert.Equal(JsonValueKind.String, snapshot["customerName"]);
        Assert.Equal(JsonValueKind.Number, snapshot["total"]);
        Assert.Equal(JsonValueKind.String, snapshot["status"]);
        Assert.Equal(JsonValueKind.String, snapshot["createdAt"]);
    }

    [Fact]
    public async Task LegacyClient_WouldBreakOnV2Shape_ProvingV2IsReallyIncompatible()
    {
        // Documenta que a mudança de v2 é de fato incompatível: o DTO do cliente v1 não lê v2.
        var http = factory.CreateClient();
        var id = await TestApi.CreateOrderAsync(http);

        var response = await http.SendAsync(TestApi.Get($"/api/orders/{id}", "2"));

        await Assert.ThrowsAsync<JsonException>(() =>
            response.Content.ReadFromJsonAsync<LegacyFrontendV1Client.LegacyOrder>(TestApi.Json));
    }
}
