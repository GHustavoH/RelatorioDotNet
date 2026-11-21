using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using RelatorioFinal.Importacao;

// executar com dotnet run -c Release
BenchmarkRunner.Run<ImportadorBenchmark>();

[MemoryDiagnoser]
public class ImportadorBenchmark
{
    private string _texto = "";

    [Params(1000, 20000)]
    public int Linhas { get; set; }

    [GlobalSetup]
    public void Preparar()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Linhas; i++)
        {
            sb.Append("ana;BRASIL;Normal;Produto").Append(i).Append(";2;10.50\n");
        }

        _texto = sb.ToString();
    }

    [Benchmark(Baseline = true)]
    public int Baseline() => ImportadorBaseline.Importar(_texto).Count;

    [Benchmark]
    public int ComSpan() => ImportadorSpan.Importar(_texto).Count;
}
