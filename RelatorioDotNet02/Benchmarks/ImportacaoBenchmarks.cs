using System.Text;
using BenchmarkDotNet.Attributes;
using HubPedidos.Application.Importacao;
using BenchmarkDotNet.Configs;

namespace HubPedidos.Benchmarks;

// Job registrado: 1 launch, 3 warmups, 15 iterações (runtime atual = net10.0).
// MemoryDiagnoser mostra bytes alocados e coletas de GC.
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 15)]
[MarkdownExporterAttribute.GitHub]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ImportacaoBenchmarks
{
    // lote pequeno e lote grande (Etapa 4)
    [Params(100, 20_000)]
    public int Pedidos { get; set; }

    private string _conteudo = string.Empty;
    private byte[] _bytes = Array.Empty<byte>();
    private long _limite;
    private readonly ImportadorAssincronoOtimizado _otimizado = new();
    private readonly ImportadorAssincronoOtimizado _otimizadoLimpando = new(limparBuffer: true);

    [GlobalSetup]
    public void Preparar()
    {
        _conteudo = GeradorLote.Gerar(Pedidos);
        _bytes = Encoding.UTF8.GetBytes(_conteudo);
        _limite = _conteudo.Length * 2L;
    }

    // ---------- Etapa 2: string (Split) x span ----------

    [Benchmark(Baseline = true), BenchmarkCategory("Sincrono")]
    public int Sync_Baseline() => ImportadorPedidosBaseline.Importar(_conteudo).Pedidos.Count;

    [Benchmark, BenchmarkCategory("Sincrono")]
    public int Sync_Span() => ImportadorPedidosOtimizado.Importar(_conteudo).Pedidos.Count;

    // ---------- Etapas 3 e 4: stream assíncrono, sem log ----------

    [Benchmark(Baseline = true), BenchmarkCategory("Async")]
    public async Task<int> Async_Baseline() =>
        (await ImportadorAssincronoBaseline.ImportarAsync(new MemoryStream(_bytes), _limite, null, CancellationToken.None)).Pedidos.Count;

    [Benchmark, BenchmarkCategory("Async")]
    public async Task<int> Async_Otimizado_Pool() =>
        (await _otimizado.ImportarAsync(new MemoryStream(_bytes), _limite, null, CancellationToken.None)).Pedidos.Count;

    [Benchmark, BenchmarkCategory("Async")]
    public async Task<int> Async_Otimizado_PoolLimpando() =>
        (await _otimizadoLimpando.ImportarAsync(new MemoryStream(_bytes), _limite, null, CancellationToken.None)).Pedidos.Count;

    // ---------- Etapa 4: com log (flush por item x agrupado) ----------

    [Benchmark(Baseline = true), BenchmarkCategory("AsyncComLog")]
    public async Task<int> AsyncLog_Baseline_FlushPorItem()
    {
        await using var log = new StreamWriter(Stream.Null, Encoding.UTF8);
        return (await ImportadorAssincronoBaseline.ImportarAsync(new MemoryStream(_bytes), _limite, log, CancellationToken.None)).Pedidos.Count;
    }

    [Benchmark, BenchmarkCategory("AsyncComLog")]
    public async Task<int> AsyncLog_Otimizado_FlushAgrupado()
    {
        await using var log = new StreamWriter(Stream.Null, Encoding.UTF8);
        return (await _otimizado.ImportarAsync(new MemoryStream(_bytes), _limite, log, CancellationToken.None)).Pedidos.Count;
    }
}
