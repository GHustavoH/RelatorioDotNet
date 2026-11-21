using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Hub.Tests.Api;

//Aplicação REAL (com v1 e v2 no mesmo processo) em ambiente Development (expõe /openapi)
public sealed class HubFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseDefaultServiceProvider(o =>
        {
            o.ValidateScopes = true;
            o.ValidateOnBuild = true;
        });
    }
}

internal static class TestApi
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<Guid> CreateOrderAsync(HttpClient client, string customer = "Maria", decimal amount = 150.75m, string currency = "BRL")
    {
        var response = await client.PostAsJsonAsync("/api/orders", new { customerName = customer, amount, currency });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    public static HttpRequestMessage Get(string url, string? versionHeader = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (versionHeader is not null)
            request.Headers.Add("X-Api-Version", versionHeader);
        return request;
    }

    public static string[] PropertyNames(JsonElement element)
        => element.EnumerateObject().Select(p => p.Name).ToArray();
}
