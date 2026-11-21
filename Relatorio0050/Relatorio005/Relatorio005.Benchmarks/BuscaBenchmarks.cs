using BenchmarkDotNet.Attributes;
using Relatorio005.Hub;

namespace Relatorio005.Benchmarks;

[MemoryDiagnoser]
public class BuscaBenchmarks
{
    private Mensagem?[] _itens = Array.Empty<Mensagem?>();

    [Params(10_000, 50_000, 200_000)]
    public int Total { get; set; }

    [Params(20)]
    public int Limite { get; set; }

    [Params("pedido", "termo-inexistente")]
    public string Termo { get; set; } = string.Empty;

    [GlobalSetup]
    public void Preparar()
    {
        _itens = GeradorMassaDados.Gerar(Total, 42);
    }

    [Benchmark(Baseline = true)]
    public int Baseline() => BuscaMensagens.Baseline(_itens, _itens.Length, "suporte", Termo, Limite).Count;

    [Benchmark]
    public int Otimizada() => BuscaMensagens.Otimizada(_itens, _itens.Length, "suporte", Termo, Limite).Count;
}
