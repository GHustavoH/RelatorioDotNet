using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Relatorio005.Hub;

public sealed class ProcessadorMensagensService : BackgroundService
{
    private readonly FilaMensagens _fila;
    private readonly MensagemStore _store;
    private readonly HubMetricas _metricas;
    private readonly HubOptions _opcoes;
    private readonly ILogger<ProcessadorMensagensService> _logger;

    public ProcessadorMensagensService(
        FilaMensagens fila,
        MensagemStore store,
        HubMetricas metricas,
        IOptions<HubOptions> opcoes,
        ILogger<ProcessadorMensagensService> logger)
    {
        _fila = fila;
        _store = store;
        _metricas = metricas;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable
            .Range(0, _opcoes.Workers)
            .Select(_ => Task.Run(() => ConsumirAsync(stoppingToken), CancellationToken.None))
            .ToArray();

        return Task.WhenAll(workers);
    }

    private async Task ConsumirAsync(CancellationToken cancelamento)
    {
        try
        {
            await foreach (var pedido in _fila.Leitor.ReadAllAsync(cancelamento))
            {
                var inicio = Stopwatch.GetTimestamp();
                _metricas.EsperaNaFilaMs.Record(Stopwatch.GetElapsedTime(pedido.EnfileiradoEm, inicio).TotalMilliseconds);

                try
                {
                    CalcularAssinatura(pedido.Conteudo, _opcoes.IteracoesHash);

                    if (_opcoes.LatenciaPersistenciaMs > 0)
                    {
                        await Task.Delay(_opcoes.LatenciaPersistenciaMs, cancelamento);
                    }

                    var mensagem = new Mensagem(
                        pedido.Id,
                        _store.ProximaSequencia(),
                        pedido.Canal,
                        pedido.Conteudo,
                        pedido.Prioridade,
                        DateTimeOffset.UtcNow);

                    if (_store.TentarAdicionar(mensagem))
                    {
                        _metricas.Processadas.Add(1);
                    }
                    else
                    {
                        _metricas.ErrosProcessamento.Add(1);
                    }
                }
                catch (OperationCanceledException) when (cancelamento.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception excecao)
                {
                    _metricas.ErrosProcessamento.Add(1);
                    _logger.LogError(excecao, "Falha ao processar a mensagem {Id}", pedido.Id);
                }
                finally
                {
                    _metricas.DuracaoProcessamentoMs.Record(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
                }
            }
        }
        catch (OperationCanceledException) when (cancelamento.IsCancellationRequested)
        {
        }
    }

    private static int CalcularAssinatura(string conteudo, int iteracoes)
    {
        if (iteracoes <= 0)
        {
            return 0;
        }

        Span<byte> atual = stackalloc byte[32];
        Span<byte> proximo = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(conteudo), atual);

        for (var i = 1; i < iteracoes; i++)
        {
            SHA256.HashData(atual, proximo);
            proximo.CopyTo(atual);
        }

        return atual[0];
    }
}
