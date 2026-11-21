using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using relatorio007.Models;
using relatorio007.Services;
using Xunit;
using Xunit.Abstractions;

namespace relatorio007.Tests;

public class PedidosHttpTests
{
    private readonly ITestOutputHelper _saida;

    public PedidosHttpTests(ITestOutputHelper saida)
    {
        _saida = saida;
    }

    private static HttpRequestMessage Get(string url, string usuario, string? ifNoneMatch = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Usuario", usuario);
        if (ifNoneMatch != null)
            req.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        return req;
    }

    private static async Task Alterar(HttpClient client, int id, string status)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/pedidos/{id}/status");
        req.Headers.Add("X-Usuario", "admin");
        req.Headers.Add("X-Perfil", "admin");
        req.Content = JsonContent.Create(new AlterarStatusRequest(status));
        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Get_Devolve_200_Com_Etag()
    {
        using var f = new WebApplicationFactory<Program>();
        var client = f.CreateClient();

        var resp = await client.SendAsync(Get("/api/pedidos/1", "ana"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.NotNull(resp.Headers.ETag);
    }

    [Fact]
    public async Task IfNoneMatch_Igual_Devolve_304_Sem_Corpo()
    {
        using var f = new WebApplicationFactory<Program>();
        var client = f.CreateClient();

        var primeira = await client.SendAsync(Get("/api/pedidos/1", "ana"));
        var etag = primeira.Headers.ETag!.ToString();

        var segunda = await client.SendAsync(Get("/api/pedidos/1", "ana", etag));

        Assert.Equal(HttpStatusCode.NotModified, segunda.StatusCode);
        Assert.Equal("", await segunda.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Etag_Obsoleto_Ou_Invalido_Devolve_200()
    {
        using var f = new WebApplicationFactory<Program>();
        var client = f.CreateClient();

        var primeira = await client.SendAsync(Get("/api/pedidos/1", "ana"));
        var etagAntigo = primeira.Headers.ETag!.ToString();

        await Alterar(client, 1, "Pago");

        var obsoleto = await client.SendAsync(Get("/api/pedidos/1", "ana", etagAntigo));
        var invalido = await client.SendAsync(Get("/api/pedidos/1", "ana", "lixo"));

        Assert.Equal(HttpStatusCode.OK, obsoleto.StatusCode);
        Assert.Equal(HttpStatusCode.OK, invalido.StatusCode);
        Assert.NotEqual(etagAntigo, obsoleto.Headers.ETag!.ToString());
    }

    [Fact]
    public async Task Outro_Usuario_Nao_Ve_O_Pedido()
    {
        using var f = new WebApplicationFactory<Program>();
        var client = f.CreateClient();

        var resp = await client.SendAsync(Get("/api/pedidos/1", "bruno"));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Leitura_Publica_Usa_Cache_E_E_Invalidada_Pela_Mudanca()
    {
        using var f = new WebApplicationFactory<Program>();
        var client = f.CreateClient();
        var store = f.Services.GetRequiredService<PedidoStore>();

        var sw = Stopwatch.StartNew();
        await client.GetAsync("/api/pedidos/1/publico");
        var tempoMiss = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        for (int i = 0; i < 9; i++)
            await client.GetAsync("/api/pedidos/1/publico");
        var tempoHits = sw.Elapsed.TotalMilliseconds / 9;

        _saida.WriteLine($"miss: {tempoMiss:F2} ms | hit (media): {tempoHits:F2} ms");
        _saida.WriteLine($"10 requisicoes, {store.LeiturasPublicas} chegaram ao store");

        Assert.Equal(1, store.LeiturasPublicas);

        await Alterar(client, 1, "Pago");

        var depois = await client.GetFromJsonAsync<PedidoPublicoDto>("/api/pedidos/1/publico");

        Assert.Equal(2, store.LeiturasPublicas);
        Assert.Equal("Pago", depois!.Status);
    }

    [Fact]
    public async Task Cache_Varia_Por_Pedido()
    {
        using var f = new WebApplicationFactory<Program>();
        var client = f.CreateClient();

        var p1 = await client.GetFromJsonAsync<PedidoPublicoDto>("/api/pedidos/1/publico");
        var p2 = await client.GetFromJsonAsync<PedidoPublicoDto>("/api/pedidos/2/publico");

        Assert.Equal(1, p1!.Id);
        Assert.Equal(2, p2!.Id);
    }

    [Fact]
    public async Task Rota_Autenticada_Nao_Fica_Em_Cache_Nem_Mistura_Usuarios()
    {
        using var f = new WebApplicationFactory<Program>();
        var client = f.CreateClient();

        var ana = await client.SendAsync(Get("/api/pedidos/1", "ana"));
        var bruno = await client.SendAsync(Get("/api/pedidos/1", "bruno"));

        Assert.Equal(HttpStatusCode.OK, ana.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, bruno.StatusCode);
    }
}
