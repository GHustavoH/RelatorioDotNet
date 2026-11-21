using System.ComponentModel.DataAnnotations;

namespace Relatorio005.Hub;

public enum EstrategiaBusca
{
    Baseline,
    Otimizada
}

public sealed class HubOptions
{
    public const string Secao = "Hub";

    public EstrategiaBusca Estrategia { get; set; } = EstrategiaBusca.Baseline;

    [Range(1, 256)]
    public int Workers { get; set; } = 2;

    [Range(1, 1_000_000)]
    public int CapacidadeFila { get; set; } = 5_000;

    [Range(1_000, 5_000_000)]
    public int CapacidadeArmazenamento { get; set; } = 300_000;

    [Range(0, 5_000_000)]
    public int SeedQuantidade { get; set; } = 50_000;

    public int SeedAleatoria { get; set; } = 42;

    [Range(0, 10_000)]
    public int LatenciaPersistenciaMs { get; set; } = 5;

    [Range(0, 100_000)]
    public int IteracoesHash { get; set; } = 200;
}
