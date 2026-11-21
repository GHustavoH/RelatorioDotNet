using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;

namespace Relatorio005.LoadTest;

public sealed class GeradorCarga
{
    private static readonly string[] Canais = { "vendas", "suporte", "financeiro", "logistica", "marketing", "rh", "ti", "juridico" };
    private static readonly string[] Termos = { "pedido", "entrega", "fatura", "cliente", "contrato", "pagamento", "estoque", "reembolso" };

    private readonly ConcurrentQueue<Amostra> _amostras = new();
    private long _emVoo;

    private readonly record struct Planejada(long PrevistoTicks, double OffsetS, Rota Rota, Fase Fase, string Caminho, object? Corpo);

    public async Task<IReadOnlyList<Amostra>> ExecutarAsync(Opcoes opcoes, CancellationToken cancelamento)
    {
        using var manipulador = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = opcoes.MaxEmVoo
        };

        using var http = new HttpClient(manipulador)
        {
            BaseAddress = new Uri(opcoes.Url),
            Timeout = Timeout.InfiniteTimeSpan
        };

        var aleatorio = new Random(opcoes.Semente);
        var duracaoTotalS = opcoes.AquecimentoS + opcoes.MedicaoS;
        var intervaloTicks = Stopwatch.Frequency / opcoes.Taxa;
        var inicio = Stopwatch.GetTimestamp();
        long indice = 0;

        try
        {
            while (!cancelamento.IsCancellationRequested)
            {
                var previsto = inicio + (long)(indice * intervaloTicks);
                var offsetS = (double)(previsto - inicio) / Stopwatch.Frequency;
                if (offsetS >= duracaoTotalS)
                {
                    break;
                }

                var restanteTicks = previsto - Stopwatch.GetTimestamp();
                if (restanteTicks > 0)
                {
                    var restanteMs = restanteTicks * 1000.0 / Stopwatch.Frequency;
                    if (restanteMs > 2)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(restanteMs - 1), cancelamento);
                    }

                    while (Stopwatch.GetTimestamp() < previsto)
                    {
                        Thread.SpinWait(20);
                    }
                }

                var planejada = Planejar(aleatorio, previsto, offsetS, opcoes, indice);
                indice++;

                if (Interlocked.Read(ref _emVoo) >= opcoes.MaxEmVoo)
                {
                    _amostras.Enqueue(new Amostra(offsetS, planejada.Rota, planejada.Fase, Resultado.DescartadaGerador, 0, 0));
                    continue;
                }

                Interlocked.Increment(ref _emVoo);
                _ = DispararAsync(http, planejada, opcoes, cancelamento);
            }
        }
        catch (OperationCanceledException)
        {
        }

        var limite = Stopwatch.GetTimestamp() + (long)((opcoes.TimeoutMs + 2000) / 1000.0 * Stopwatch.Frequency);
        while (Interlocked.Read(ref _emVoo) > 0 && Stopwatch.GetTimestamp() < limite)
        {
            await Task.Delay(50, CancellationToken.None);
        }

        return _amostras.ToArray();
    }

    private static Planejada Planejar(Random aleatorio, long previsto, double offsetS, Opcoes opcoes, long indice)
    {
        var fase = offsetS < opcoes.AquecimentoS ? Fase.Aquecimento : Fase.Medicao;
        var canal = Canais[aleatorio.Next(Canais.Length)];

        if (aleatorio.NextDouble() < opcoes.MixCriacao)
        {
            var corpo = new
            {
                canal,
                conteudo = $"mensagem de carga {indice} {Termos[aleatorio.Next(Termos.Length)]} {Termos[aleatorio.Next(Termos.Length)]}",
                prioridade = aleatorio.Next(0, 10)
            };

            return new Planejada(previsto, offsetS, Rota.Criacao, fase, "/api/mensagens", corpo);
        }

        var termo = Termos[aleatorio.Next(Termos.Length)];
        return new Planejada(previsto, offsetS, Rota.Consulta, fase, $"/api/mensagens/busca?canal={canal}&termo={termo}&limite=20", null);
    }

    private async Task DispararAsync(HttpClient http, Planejada planejada, Opcoes opcoes, CancellationToken cancelamento)
    {
        try
        {
            await EnviarAsync(http, planejada, opcoes, cancelamento);
        }
        finally
        {
            Interlocked.Decrement(ref _emVoo);
        }
    }

    private async Task EnviarAsync(HttpClient http, Planejada planejada, Opcoes opcoes, CancellationToken cancelamento)
    {
        using var limiteTempo = CancellationTokenSource.CreateLinkedTokenSource(cancelamento);
        limiteTempo.CancelAfter(opcoes.TimeoutMs);

        var status = 0;
        Resultado resultado;

        try
        {
            using var resposta = planejada.Rota == Rota.Criacao
                ? await http.PostAsJsonAsync(planejada.Caminho, planejada.Corpo, limiteTempo.Token)
                : await http.GetAsync(planejada.Caminho, limiteTempo.Token);

            status = (int)resposta.StatusCode;
            resultado = status is >= 200 and < 300
                ? Resultado.Sucesso
                : status is 503 or 429
                    ? Resultado.RejeitadaServidor
                    : Resultado.ErroHttp;
        }
        catch (OperationCanceledException) when (cancelamento.IsCancellationRequested)
        {
            return;
        }
        catch (OperationCanceledException)
        {
            resultado = Resultado.Timeout;
        }
        catch (HttpRequestException)
        {
            resultado = Resultado.ErroConexao;
        }

        var latenciaMs = Stopwatch.GetElapsedTime(planejada.PrevistoTicks).TotalMilliseconds;
        _amostras.Enqueue(new Amostra(planejada.OffsetS, planejada.Rota, planejada.Fase, resultado, status, latenciaMs));
    }
}
