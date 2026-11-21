using System.Net.Http.Json;
using System.Text.Json;
using Hub.Tests.Api;

namespace Hub.Tests.Compatibility;

internal sealed class LegacyFrontendV1Client(HttpClient http)
{
    //Formato que o front-end v1 espera. "Total" é número
    public sealed class LegacyOrder
    {
        public Guid Id { get; set; }
        public string CustomerName { get; set; } = "";
        public decimal Total { get; set; }
        public string Status { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
    }

    public async Task<Guid> PlaceOrderAsync(string customerName, decimal amount)
    {
        var response = await http.PostAsJsonAsync("/api/orders", new { customerName, amount, currency = "BRL" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    public async Task<LegacyOrder> GetOrderAsync(Guid id)
    {
        var response = await http.GetAsync($"/api/orders/{id}");
        response.EnsureSuccessStatusCode();

        // Desserialização estrita no formato v1: lança JsonException se "total" deixar de ser número.
        return (await response.Content.ReadFromJsonAsync<LegacyOrder>(TestApi.Json))
               ?? throw new InvalidOperationException("Resposta vazia");
    }
}
