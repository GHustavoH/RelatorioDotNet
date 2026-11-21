using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using RelatorioFinal.Contratos;
using RelatorioFinal.Dominio;
using RelatorioFinal.Persistencia;
using RelatorioFinal.Servicos;

namespace RelatorioFinal.Fila;

public class ProcessadorPedidosWorker(
    Channel<ProcessarPedidoJob> canal,
    IServiceScopeFactory escopos,
    MetricasFila metricas,
    IConfiguration config,
    ILogger<ProcessadorPedidosWorker> log) : BackgroundService
{
    private static readonly ActivitySource Fonte = new("RelatorioFinal");

    // limita quantos consumidores usam o banco ao mesmo tempo
    private readonly SemaphoreSlim _limiteBanco = new(config.GetValue("Fila:LimiteBanco", 2));

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Yield();
        await RecarregarPendentesAsync(ct);

        var quantidade = config.GetValue("Fila:Consumidores", 2);
        var consumidores = Enumerable.Range(0, quantidade).Select(_ => ConsumirAsync(ct)).ToList();
        await Task.WhenAll(consumidores);
    }

    // politica de encerramento e cancelar a leitura, o que ficou pendente continua Recebido no banco e volta para a fila na proxima inicializacao
    private async Task RecarregarPendentesAsync(CancellationToken ct)
    {
        try
        {
            using var escopo = escopos.CreateScope();
            var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
            var ids = await db.Pedidos
                .Where(p => p.Status == "Recebido")
                .OrderBy(p => p.CriadoEm)
                .Select(p => p.Id)
                .ToListAsync(ct);

            foreach (var id in ids)
            {
                canal.Writer.TryWrite(new ProcessarPedidoJob(id, DateTime.UtcNow));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Falha ao recarregar pedidos pendentes");
        }
    }

    private async Task ConsumirAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var job in canal.Reader.ReadAllAsync(ct))
            {
                await ProcessarAsync(job, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // parada normal da aplicacao
        }
    }

    private async Task ProcessarAsync(ProcessarPedidoJob job, CancellationToken ct)
    {
        var relogio = Stopwatch.StartNew();
        using var atividade = Fonte.StartActivity("processar-pedido");
        atividade?.SetTag("pedido.id", job.PedidoId);

        try
        {
            // escopo novo por item porque o DbContext e scoped
            using var escopo = escopos.CreateScope();
            var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
            var catalogo = escopo.ServiceProvider.GetRequiredService<CatalogoProcessadores>();
            var notificador = escopo.ServiceProvider.GetRequiredService<INotificadorPedidos>();
            var cache = escopo.ServiceProvider.GetRequiredService<IOutputCacheStore>();

            // simula trabalho externo fora do semaforo para nao segurar o slot a toa
            var atraso = config.GetValue("Fila:AtrasoSimuladoMs", 0);
            if (atraso > 0)
            {
                await Task.Delay(atraso, ct);
            }

            PedidoAtualizado evento;

            await _limiteBanco.WaitAsync(ct);
            try
            {
                var pedido = await db.Pedidos.Include(p => p.Itens).FirstOrDefaultAsync(p => p.Id == job.PedidoId, ct);

                // so processa se ainda estiver Recebido o que evita processar duas vezes
                if (pedido is null || pedido.Status != "Recebido")
                {
                    metricas.Incrementar("ignorados");
                    return;
                }

                var dominio = PedidoMapeador.EntidadeParaDominio(pedido);
                var processador = catalogo.Obter(Classificador.Classificar(dominio));
                var resultado = processador.Processar(dominio);

                pedido.Status = "Processado";
                pedido.AtualizadoEm = DateTime.UtcNow;
                pedido.Metadados = new MetadadosPedido { Origem = pedido.Metadados.Origem, Observacao = resultado };
                await db.SaveChangesAsync(ct);

                evento = new PedidoAtualizado(pedido.Id, pedido.Status, pedido.Categoria, pedido.AtualizadoEm);
            }
            finally
            {
                _limiteBanco.Release();
            }

            // notifica somente depois que a mudanca foi persistida
            await cache.EvictByTagAsync("pedidos", ct);
            await notificador.NotificarAsync(evento, ct);
            metricas.Incrementar("processados");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            metricas.Incrementar("falhas");
            log.LogError(ex, "Falha ao processar o pedido {PedidoId}", job.PedidoId);
        }
        finally
        {
            metricas.Duracao.Record(relogio.Elapsed.TotalMilliseconds);
        }
    }
}
