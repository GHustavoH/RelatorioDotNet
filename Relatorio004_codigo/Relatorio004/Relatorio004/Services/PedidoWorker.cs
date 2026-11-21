using System.Diagnostics;
using System.Threading.Channels;
using Relatorio004.Models;

namespace Relatorio004.Services;

public class PedidoWorker : BackgroundService
{
    private readonly Channel<ProcessarPedidoJob> _canal;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Metricas _metricas;
    private readonly ILogger<PedidoWorker> _logger;
    private readonly int _consumidores;
    private readonly bool _drenar;
    private readonly SemaphoreSlim _semaforo;
    private readonly CancellationTokenSource _forcar = new();

    public PedidoWorker(Channel<ProcessarPedidoJob> canal, IServiceScopeFactory scopeFactory,
        Metricas metricas, ILogger<PedidoWorker> logger, IConfiguration config)
    {
        _canal = canal;
        _scopeFactory = scopeFactory;
        _metricas = metricas;
        _logger = logger;
        _consumidores = config.GetValue<int>("Fila:Consumidores", 2);
        _drenar = config.GetValue<bool>("Fila:DrenarNoShutdown", false);

        int limiteBanco = config.GetValue<int>("Fila:LimiteBanco", 2);
        _semaforo = new SemaphoreSlim(limiteBanco, limiteBanco);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // modo drenar: os consumidores ignoram o stoppingToken e param so quando a fila esvazia
        // (ou quando o tempo do shutdown estoura, que cancela o _forcar)
        // modo cancelar: param assim que o host pedir para parar
        var token = _drenar ? _forcar.Token : stoppingToken;

        var tarefas = new List<Task>();
        for (int i = 1; i <= _consumidores; i++)
        {
            int numero = i;
            tarefas.Add(Task.Run(() => Consumir(numero, token)));
        }

        await Task.WhenAll(tarefas);

        // o que sobrou na fila nao sera processado: registra para ficar visivel
        while (_canal.Reader.TryRead(out var sobra))
        {
            _metricas.Somar("pendentesNaoProcessados");
            _logger.LogWarning("Pedido {Id} ficou pendente no shutdown", sobra.Id);
        }
    }

    private async Task Consumir(int numero, CancellationToken ct)
    {
        _logger.LogInformation("Consumidor {N} iniciado", numero);

        try
        {
            await foreach (var job in _canal.Reader.ReadAllAsync(ct))
            {
                await ProcessarJob(job, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // aplicacao parando
        }

        _logger.LogInformation("Consumidor {N} encerrado", numero);
    }

    private async Task ProcessarJob(ProcessarPedidoJob job, CancellationToken ct)
    {
        // tempo que o job ficou esperando na fila
        long esperaMs = (long)(DateTime.UtcNow - job.CriadoEm).TotalMilliseconds;
        _metricas.Somar("esperaTotalMs", esperaMs);

        var relogio = Stopwatch.StartNew();

        try
        {
            // um escopo novo para cada item
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPedidoRepositorio>();

            // so segura o slot durante o acesso ao banco
            await _semaforo.WaitAsync(ct);
            _metricas.EntrouNoBanco();
            try
            {
                await repo.SalvarAsync(job.Id, job.Cliente, job.Valor, ct);
            }
            finally
            {
                _metricas.SaiuDoBanco();
                _semaforo.Release();
            }

            _metricas.Somar("processados");
            _logger.LogInformation("Pedido {Id} ok em {Ms}ms (esperou {Espera}ms)",
                job.Id, relogio.ElapsedMilliseconds, esperaMs);
        }
        catch (OperationCanceledException)
        {
            // item que estava sendo processado quando mandaram parar
            _metricas.Somar("interrompidos");
            _logger.LogWarning("Pedido {Id} interrompido pelo cancelamento", job.Id);
            throw;
        }
        catch (Exception ex)
        {
            // a falha de um item nao derruba o consumidor, so fica registrada
            _metricas.Somar("falhas");
            _logger.LogError(ex, "Pedido {Id} falhou em {Ms}ms", job.Id, relogio.ElapsedMilliseconds);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // 1) fecha a fila: a partir daqui ninguem mais consegue escrever
        _canal.Writer.TryComplete();

        // 2) se o tempo do shutdown acabar, forca os consumidores a pararem
        using var registro = cancellationToken.Register(() => _forcar.Cancel());

        // 3) no modo drenar espera a fila esvaziar antes de cancelar o resto
        if (_drenar && ExecuteTask != null)
        {
            await Task.WhenAny(ExecuteTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }

        await base.StopAsync(cancellationToken);
    }
}
