namespace Relatorio004.Tests;

using Relatorio004.Services;

public class MetricasTests
{
    [Fact]
    public void Somar_ComVariasThreadsNaoPerdeContagem()
    {
        var metricas = new Metricas();

        Parallel.For(0, 10000, i => metricas.Somar("teste"));

        Assert.Equal(10000, metricas.Valor("teste"));
    }

    [Fact]
    public void MaxConcorrencia_RegistraOPico()
    {
        var metricas = new Metricas();

        metricas.EntrouNoBanco();
        metricas.EntrouNoBanco();
        metricas.SaiuDoBanco();
        metricas.EntrouNoBanco();
        metricas.SaiuDoBanco();
        metricas.SaiuDoBanco();

        Assert.Equal(2, metricas.Valor("maxConcorrenciaBanco"));
    }
}
