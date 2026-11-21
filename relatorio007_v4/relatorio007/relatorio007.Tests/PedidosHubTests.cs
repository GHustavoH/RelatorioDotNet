using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using relatorio007.Models;
using Xunit;

namespace relatorio007.Tests;

public class PedidosHubTests
{
    private static HubConnection Criar(WebApplicationFactory<Program> f, string usuario)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(f.Server.BaseAddress, "hubs/pedidos"), o =>
            {
                o.HttpMessageHandlerFactory = _ => f.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                o.Headers["X-Usuario"] = usuario;
            })
            .Build();
    }

    private static async Task Alterar(WebApplicationFactory<Program> f, int id, string status)
    {
        var client = f.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/pedidos/{id}/status");
        req.Headers.Add("X-Usuario", "admin");
        req.Headers.Add("X-Perfil", "admin");
        req.Content = JsonContent.Create(new AlterarStatusRequest(status));
        (await client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private static async Task<PedidoAtualizado?> Esperar(TaskCompletionSource<PedidoAtualizado> tcs, int ms = 2000)
    {
        var fim = await Task.WhenAny(tcs.Task, Task.Delay(ms));
        return fim == tcs.Task ? tcs.Task.Result : null;
    }

    [Fact]
    public async Task Cliente_Autorizado_Recebe_O_Evento()
    {
        using var f = new WebApplicationFactory<Program>();
        var ana = Criar(f, "ana");
        var tcs = new TaskCompletionSource<PedidoAtualizado>();
        ana.On<PedidoAtualizado>("PedidoAtualizado", e => tcs.TrySetResult(e));

        await ana.StartAsync();
        await ana.InvokeAsync("Entrar", 1);

        await Alterar(f, 1, "Pago");

        var evento = await Esperar(tcs);
        Assert.NotNull(evento);
        Assert.Equal(1, evento!.PedidoId);
        Assert.Equal("Pago", evento.Status);
        Assert.Equal(2, evento.Versao);

        await ana.StopAsync();
    }

    [Fact]
    public async Task Cliente_Nao_Autorizado_Nao_Entra_No_Grupo()
    {
        using var f = new WebApplicationFactory<Program>();
        var bruno = Criar(f, "bruno");
        var tcs = new TaskCompletionSource<PedidoAtualizado>();
        bruno.On<PedidoAtualizado>("PedidoAtualizado", e => tcs.TrySetResult(e));

        await bruno.StartAsync();

        await Assert.ThrowsAsync<HubException>(() => bruno.InvokeAsync("Entrar", 1));

        await Alterar(f, 1, "Pago");

        Assert.Null(await Esperar(tcs, 500));
        await bruno.StopAsync();
    }

    [Fact]
    public async Task Evento_So_Chega_No_Grupo_Do_Pedido()
    {
        using var f = new WebApplicationFactory<Program>();
        var bruno = Criar(f, "bruno");
        var tcs = new TaskCompletionSource<PedidoAtualizado>();
        bruno.On<PedidoAtualizado>("PedidoAtualizado", e => tcs.TrySetResult(e));

        await bruno.StartAsync();
        await bruno.InvokeAsync("Entrar", 2);

        await Alterar(f, 1, "Pago");

        Assert.Null(await Esperar(tcs, 500));
        await bruno.StopAsync();
    }

    [Fact]
    public async Task Revogar_Remove_O_Cliente_Do_Grupo()
    {
        using var f = new WebApplicationFactory<Program>();
        var ana = Criar(f, "ana");
        var tcs = new TaskCompletionSource<PedidoAtualizado>();
        ana.On<PedidoAtualizado>("PedidoAtualizado", e => tcs.TrySetResult(e));

        await ana.StartAsync();
        await ana.InvokeAsync("Entrar", 1);

        var client = f.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/pedidos/1/revogar/ana");
        req.Headers.Add("X-Usuario", "admin");
        req.Headers.Add("X-Perfil", "admin");
        (await client.SendAsync(req)).EnsureSuccessStatusCode();

        await Alterar(f, 1, "Pago");

        Assert.Null(await Esperar(tcs, 500));
        await ana.StopAsync();
    }
}
