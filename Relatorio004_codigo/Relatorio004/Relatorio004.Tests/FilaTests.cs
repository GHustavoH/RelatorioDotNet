using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Relatorio004.Controllers;
using Relatorio004.Models;
using Relatorio004.Services;

namespace Relatorio004.Tests;

public class FilaTests
{
    private static List<ProcessarPedidoJob> LerTudo(Channel<ProcessarPedidoJob> canal)
    {
        var lista = new List<ProcessarPedidoJob>();
        while (canal.Reader.TryRead(out var job)) lista.Add(job);
        return lista;
    }

    // ---------- modo Wait ----------

    [Fact]
    public async Task Wait_ProdutorAguardaAteAbrirVaga()
    {
        var metricas = new Metricas();
        var canal = FilaFactory.Criar(1, BoundedChannelFullMode.Wait, metricas);
        await canal.Writer.WriteAsync(Ajudantes.NovoJob());

        var escrita = canal.Writer.WriteAsync(Ajudantes.NovoJob()).AsTask();

        await Task.Delay(200);
        Assert.False(escrita.IsCompleted); // ficou esperando

        canal.Reader.TryRead(out _); // abre vaga

        await escrita.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(escrita.IsCompletedSuccessfully);
        Assert.Equal(0, metricas.Valor("descartados")); // nada foi perdido
    }

    [Fact]
    public async Task Wait_EndpointAguardarEsperaEDepoisAceita()
    {
        var metricas = new Metricas();
        var canal = FilaFactory.Criar(1, BoundedChannelFullMode.Wait, metricas);
        var controller = new PedidosController(canal, metricas, Ajudantes.Config(capacidade: 1));
        var req = new PedidoRequest { Cliente = "a", Valor = 10 };

        await controller.CriarAguardando(req, CancellationToken.None); // enche
        var segunda = controller.CriarAguardando(req, CancellationToken.None);

        await Task.Delay(200);
        Assert.False(segunda.IsCompleted);

        canal.Reader.TryRead(out _);
        var resultado = await segunda.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsType<AcceptedResult>(resultado);
    }

    // ---------- modos de descarte ----------

    [Fact]
    public void DropWrite_DescartaOItemNovo()
    {
        var metricas = new Metricas();
        var canal = FilaFactory.Criar(2, BoundedChannelFullMode.DropWrite, metricas);
        var jobs = Enumerable.Range(1, 5).Select(i => Ajudantes.NovoJob("c" + i)).ToList();

        foreach (var j in jobs) canal.Writer.TryWrite(j);

        var restantes = LerTudo(canal).Select(j => j.Cliente).ToList();
        Assert.Equal(new[] { "c1", "c2" }, restantes);
        Assert.Equal(3, metricas.Valor("descartados"));
    }

    [Fact]
    public void DropOldest_DescartaOMaisAntigo()
    {
        var metricas = new Metricas();
        var canal = FilaFactory.Criar(2, BoundedChannelFullMode.DropOldest, metricas);

        for (int i = 1; i <= 5; i++) canal.Writer.TryWrite(Ajudantes.NovoJob("c" + i));

        var restantes = LerTudo(canal).Select(j => j.Cliente).ToList();
        Assert.Equal(new[] { "c4", "c5" }, restantes);
        Assert.Equal(3, metricas.Valor("descartados"));
    }

    [Fact]
    public void DropNewest_DescartaOMaisNovoQueJaEstavaNaFila()
    {
        var metricas = new Metricas();
        var canal = FilaFactory.Criar(2, BoundedChannelFullMode.DropNewest, metricas);

        for (int i = 1; i <= 5; i++) canal.Writer.TryWrite(Ajudantes.NovoJob("c" + i));

        var restantes = LerTudo(canal).Select(j => j.Cliente).ToList();
        Assert.Equal(new[] { "c1", "c5" }, restantes);
        Assert.Equal(3, metricas.Valor("descartados"));
    }

    // ---------- rejeicao explicita: TryWrite + HTTP ----------

    [Fact]
    public void TryWrite_FilaCheiaDevolve429()
    {
        var metricas = new Metricas();
        var canal = FilaFactory.Criar(1, BoundedChannelFullMode.Wait, metricas);
        var controller = new PedidosController(canal, metricas, Ajudantes.Config(capacidade: 1));
        var req = new PedidoRequest { Cliente = "a", Valor = 10 };

        var primeira = controller.Criar(req);
        var segunda = controller.Criar(req);

        Assert.IsType<AcceptedResult>(primeira);
        var objeto = Assert.IsType<ObjectResult>(segunda);
        Assert.Equal(429, objeto.StatusCode);

        Assert.Equal(1, metricas.Valor("aceitos"));
        Assert.Equal(1, metricas.Valor("rejeitados"));
        Assert.Equal(0, metricas.Valor("descartados")); // rejeitado e descartado sao coisas diferentes
    }

    [Fact]
    public void TryWrite_FilaFechadaDevolve503()
    {
        var metricas = new Metricas();
        var canal = FilaFactory.Criar(5, BoundedChannelFullMode.Wait, metricas);
        var controller = new PedidosController(canal, metricas, Ajudantes.Config());
        canal.Writer.TryComplete();

        var resultado = controller.Criar(new PedidoRequest { Cliente = "a", Valor = 10 });

        var objeto = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(503, objeto.StatusCode);
    }

    [Fact]
    public async Task Cancelamento_ProdutorEsperandoRecebe499()
    {
        var metricas = new Metricas();
        var canal = FilaFactory.Criar(1, BoundedChannelFullMode.Wait, metricas);
        var controller = new PedidosController(canal, metricas, Ajudantes.Config(capacidade: 1));
        var req = new PedidoRequest { Cliente = "a", Valor = 10 };
        await controller.CriarAguardando(req, CancellationToken.None); // enche

        using var cts = new CancellationTokenSource();
        var espera = controller.CriarAguardando(req, cts.Token);
        await Task.Delay(100);
        cts.Cancel();

        var resultado = await espera.WaitAsync(TimeSpan.FromSeconds(2));
        var status = Assert.IsType<StatusCodeResult>(resultado);
        Assert.Equal(499, status.StatusCode);
        Assert.Equal(1, canal.Reader.Count); // o item cancelado nao entrou
    }
}
