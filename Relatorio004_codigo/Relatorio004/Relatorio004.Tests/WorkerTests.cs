using System.Threading.Channels;
using Relatorio004.Services;

namespace Relatorio004.Tests;

public class WorkerTests
{
    // ---------- limite de concorrencia no banco ----------

    [Fact]
    public async Task Banco_NuncaPassaDoLimiteDoSemaforo()
    {
        var metricas = new Metricas();
        var canal = FilaFactory.Criar(50, BoundedChannelFullMode.Wait, metricas);
        var repo = new FakeRepositorio { DelayMs = 50 };
        var worker = Ajudantes.CriarWorker(canal, repo, metricas, consumidores: 8, limiteBanco: 2, drenar: false);

        for (int i = 0; i < 20; i++) canal.Writer.TryWrite(Ajudantes.NovoJob());

        await worker.StartAsync(CancellationToken.None);
        Assert.True(await Ajudantes.Esperar(() => metricas.Valor("processados") == 20));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(repo.MaxEmUso <= 2);
        Assert.Equal(2, repo.MaxEmUso); // chegou no teto, entao o teste de fato exercitou o limite
        Assert.True(metricas.Valor("maxConcorrenciaBanco") <= 2);
    }

    [Fact]
    public async Task Banco_ExcecaoNaoPerdeSlotDoSemaforo()
    {
        var metricas = new Metricas();
        var canal = FilaFactory.Criar(50, BoundedChannelFullMode.Wait, metricas);
        var repo = new FakeRepositorio { DelayMs = 10 };
        var worker = Ajudantes.CriarWorker(canal, repo, metricas, consumidores: 3, limiteBanco: 2, drenar: false);

        // 6 falhas seguidas com so 2 slots: se algum slot vazasse, o resto travaria
        for (int i = 0; i < 6; i++) canal.Writer.TryWrite(Ajudantes.NovoJob("erro"));
        for (int i = 0; i < 10; i++) canal.Writer.TryWrite(Ajudantes.NovoJob());

        await worker.StartAsync(CancellationToken.None);
        Assert.True(await Ajudantes.Esperar(() => metricas.Valor("processados") == 10));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(6, metricas.Valor("falhas"));
    }

    // ---------- falha de um item ----------

    [Fact]
    public async Task Falha_NaoEncerraOsConsumidores()
    {
        var metricas = new Metricas();
        var canal = FilaFactory.Criar(50, BoundedChannelFullMode.Wait, metricas);
        var repo = new FakeRepositorio { DelayMs = 10 };
        var worker = Ajudantes.CriarWorker(canal, repo, metricas, consumidores: 2, limiteBanco: 2, drenar: false);

        // as duas primeiras falham (uma para cada consumidor)
        canal.Writer.TryWrite(Ajudantes.NovoJob("erro"));
        canal.Writer.TryWrite(Ajudantes.NovoJob("erro"));
        for (int i = 0; i < 10; i++) canal.Writer.TryWrite(Ajudantes.NovoJob());

        await worker.StartAsync(CancellationToken.None);
        Assert.True(await Ajudantes.Esperar(() => metricas.Valor("processados") == 10));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(2, metricas.Valor("falhas")); // a falha ficou visivel
        Assert.Equal(10, metricas.Valor("processados")); // e os consumidores continuaram vivos
    }

    // ---------- cancelamento ----------

    [Fact]
    public async Task Cancelamento_ImpedeNovosItensERegistraPendentes()
    {
        var metricas = new Metricas();
        var canal = FilaFactory.Criar(50, BoundedChannelFullMode.Wait, metricas);
        var repo = new FakeRepositorio { DelayMs = 300 };
        var worker = Ajudantes.CriarWorker(canal, repo, metricas, consumidores: 1, limiteBanco: 1, drenar: false);

        for (int i = 0; i < 10; i++) canal.Writer.TryWrite(Ajudantes.NovoJob());

        await worker.StartAsync(CancellationToken.None);
        Assert.True(await Ajudantes.Esperar(() => repo.Iniciados >= 1));
        await worker.StopAsync(CancellationToken.None);

        // nao entra mais nada
        Assert.False(canal.Writer.TryWrite(Ajudantes.NovoJob()));

        // estrategia explicita para os pendentes: contados e logados, nao somem em silencio
        long total = metricas.Valor("processados") + metricas.Valor("falhas")
                   + metricas.Valor("interrompidos") + metricas.Valor("pendentesNaoProcessados");
        Assert.Equal(10, total);
        Assert.True(metricas.Valor("pendentesNaoProcessados") > 0);
    }

    [Fact]
    public async Task Drenar_ProcessaTudoQueJaEstavaNaFilaAntesDeParar()
    {
        var metricas = new Metricas();
        var canal = FilaFactory.Criar(50, BoundedChannelFullMode.Wait, metricas);
        var repo = new FakeRepositorio { DelayMs = 20 };
        var worker = Ajudantes.CriarWorker(canal, repo, metricas, consumidores: 2, limiteBanco: 2, drenar: true);

        for (int i = 0; i < 10; i++) canal.Writer.TryWrite(Ajudantes.NovoJob());

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(10, metricas.Valor("processados"));
        Assert.Equal(0, metricas.Valor("pendentesNaoProcessados"));
        Assert.False(canal.Writer.TryWrite(Ajudantes.NovoJob())); // fila fechada
    }

    // ---------- sem processar duas vezes ----------

    [Fact]
    public async Task NenhumPedidoEProcessadoDuasVezes()
    {
        var metricas = new Metricas();
        var canal = FilaFactory.Criar(200, BoundedChannelFullMode.Wait, metricas);
        var repo = new FakeRepositorio { DelayMs = 5 };
        var worker = Ajudantes.CriarWorker(canal, repo, metricas, consumidores: 4, limiteBanco: 4, drenar: false);

        for (int i = 0; i < 100; i++) canal.Writer.TryWrite(Ajudantes.NovoJob());

        await worker.StartAsync(CancellationToken.None);
        Assert.True(await Ajudantes.Esperar(() => metricas.Valor("processados") == 100));
        await worker.StopAsync(CancellationToken.None);

        var ids = repo.Salvos.ToList();
        Assert.Equal(100, ids.Count);
        Assert.Equal(100, ids.Distinct().Count());
    }
}
