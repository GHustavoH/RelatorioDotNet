namespace Relatorio005.LoadTest;

public static class Estatisticas
{
    public static double Percentil(double[] ordenado, double percentual)
    {
        if (ordenado.Length == 0)
        {
            return double.NaN;
        }

        var indice = (int)Math.Ceiling(percentual / 100.0 * ordenado.Length) - 1;
        return ordenado[Math.Clamp(indice, 0, ordenado.Length - 1)];
    }

    public static ResumoRota Resumir(IReadOnlyCollection<Amostra> amostras, double duracaoMedicaoS)
    {
        var sucessos = amostras
            .Where(a => a.Resultado == Resultado.Sucesso)
            .Select(a => a.LatenciaMs)
            .Order()
            .ToArray();

        var concluidas = amostras
            .Where(a => a.Resultado != Resultado.DescartadaGerador)
            .Select(a => a.LatenciaMs)
            .Order()
            .ToArray();

        var emitidas = amostras.Count;

        return new ResumoRota(
            emitidas,
            sucessos.Length,
            amostras.Count(a => a.Resultado == Resultado.RejeitadaServidor),
            amostras.Count(a => a.Resultado == Resultado.ErroHttp),
            amostras.Count(a => a.Resultado == Resultado.Timeout),
            amostras.Count(a => a.Resultado == Resultado.ErroConexao),
            amostras.Count(a => a.Resultado == Resultado.DescartadaGerador),
            emitidas == 0 ? 0 : (emitidas - sucessos.Length) / (double)emitidas,
            duracaoMedicaoS <= 0 ? 0 : sucessos.Length / duracaoMedicaoS,
            Percentil(sucessos, 50),
            Percentil(sucessos, 95),
            Percentil(sucessos, 99),
            sucessos.Length == 0 ? double.NaN : sucessos[^1],
            sucessos.Length == 0 ? double.NaN : sucessos.Average(),
            Percentil(concluidas, 95),
            Percentil(concluidas, 99));
    }

    public static IReadOnlyList<MetaItem> AvaliarMetas(
        MetasConfig metas,
        double taxaAlvo,
        ResumoRota geral,
        ResumoRota criacao,
        ResumoRota consulta)
    {
        var itens = new List<MetaItem>();

        void Maximo(string nome, double medido, double limite)
        {
            if (limite > 0)
            {
                itens.Add(new MetaItem(nome, medido, limite, "<=", !double.IsNaN(medido) && medido <= limite));
            }
        }

        Maximo("Consulta p95 (ms)", consulta.P95Ms, metas.Consulta.P95Ms);
        Maximo("Consulta p99 (ms)", consulta.P99Ms, metas.Consulta.P99Ms);
        Maximo("Criacao p95 (ms)", criacao.P95Ms, metas.Criacao.P95Ms);
        Maximo("Criacao p99 (ms)", criacao.P99Ms, metas.Criacao.P99Ms);
        Maximo("Taxa de erro", geral.TaxaErro, metas.TaxaErroMaxima);

        if (metas.VazaoMinimaRelativa > 0)
        {
            var minimo = taxaAlvo * metas.VazaoMinimaRelativa;
            itens.Add(new MetaItem("Vazao (req/s)", geral.VazaoRps, minimo, ">=", geral.VazaoRps >= minimo));
        }

        return itens;
    }
}
