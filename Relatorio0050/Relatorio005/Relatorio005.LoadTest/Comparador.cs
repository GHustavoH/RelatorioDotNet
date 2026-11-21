using System.Text.Json;

namespace Relatorio005.LoadTest;

public static class Comparador
{
    public static int Executar(string arquivoAntes, string arquivoDepois)
    {
        var antes = Ler(arquivoAntes);
        var depois = Ler(arquivoDepois);

        Console.WriteLine($"Antes : {antes.Rotulo} (estrategia {Estrategia(antes)})");
        Console.WriteLine($"Depois: {depois.Rotulo} (estrategia {Estrategia(depois)})");

        var parametrosIguais =
            antes.Parametros.Taxa == depois.Parametros.Taxa &&
            antes.Parametros.AquecimentoS == depois.Parametros.AquecimentoS &&
            antes.Parametros.MedicaoS == depois.Parametros.MedicaoS &&
            antes.Parametros.MixCriacao == depois.Parametros.MixCriacao &&
            antes.Parametros.Semente == depois.Parametros.Semente;

        if (!parametrosIguais)
        {
            Console.WriteLine("ATENCAO: os parametros de carga das duas execucoes sao diferentes; a comparacao nao e valida.");
        }

        if (antes.Ambiente.Processador != depois.Ambiente.Processador || antes.Ambiente.NucleosLogicos != depois.Ambiente.NucleosLogicos)
        {
            Console.WriteLine("ATENCAO: as execucoes foram feitas em ambientes diferentes.");
        }

        Console.WriteLine();
        Console.WriteLine($"{"Metrica",-28}{"Antes",14}{"Depois",14}{"Variacao",12}  Leitura");

        Linha("Consulta p50 (ms)", antes.Consulta.P50Ms, depois.Consulta.P50Ms, true);
        Linha("Consulta p95 (ms)", antes.Consulta.P95Ms, depois.Consulta.P95Ms, true);
        Linha("Consulta p99 (ms)", antes.Consulta.P99Ms, depois.Consulta.P99Ms, true);
        Linha("Criacao p50 (ms)", antes.Criacao.P50Ms, depois.Criacao.P50Ms, true);
        Linha("Criacao p95 (ms)", antes.Criacao.P95Ms, depois.Criacao.P95Ms, true);
        Linha("Criacao p99 (ms)", antes.Criacao.P99Ms, depois.Criacao.P99Ms, true);
        Linha("Vazao (req/s)", antes.Geral.VazaoRps, depois.Geral.VazaoRps, false);
        Linha("Taxa de erro", antes.Geral.TaxaErro, depois.Geral.TaxaErro, true);
        Linha("Timeouts", antes.Geral.Timeouts, depois.Geral.Timeouts, true);
        Linha("Rejeicoes do servidor", antes.Geral.Rejeicoes, depois.Geral.Rejeicoes, true);
        Linha("Descartadas no gerador", antes.Geral.DescartadasGerador, depois.Geral.DescartadasGerador, true);

        Console.WriteLine();
        Console.WriteLine("Metas na execucao 'depois':");
        foreach (var meta in depois.Metas)
        {
            Console.WriteLine($"  [{(meta.Atingida ? "OK" : "FALHOU")}] {meta.Nome}: medido {meta.Medido:F3} {meta.Comparacao} {meta.Limite:F3}");
        }

        return 0;
    }

    private static ResultadoExecucao Ler(string arquivo) =>
        JsonSerializer.Deserialize<ResultadoExecucao>(File.ReadAllText(arquivo), ConfiguracaoJson.Serializacao)
        ?? throw new InvalidOperationException($"Arquivo invalido: {arquivo}");

    private static string Estrategia(ResultadoExecucao resultado) =>
        resultado.ServidorAntes is { ValueKind: JsonValueKind.Object } servidor &&
        servidor.TryGetProperty("estrategia", out var valor)
            ? valor.ToString()
            : "desconhecida";

    private static void Linha(string nome, double antes, double depois, bool menorEMelhor)
    {
        if (double.IsNaN(antes) || double.IsNaN(depois))
        {
            Console.WriteLine($"{nome,-28}{Formatar(antes),14}{Formatar(depois),14}{"n/d",12}  sem dados");
            return;
        }

        var variacao = antes == 0 ? (depois == 0 ? 0 : double.PositiveInfinity) : (depois - antes) / antes * 100;
        var melhorou = menorEMelhor ? depois < antes * 0.95 : depois > antes * 1.05;
        var piorou = menorEMelhor ? depois > antes * 1.05 : depois < antes * 0.95;
        var leitura = melhorou ? "melhorou" : piorou ? "piorou" : "sem mudanca relevante (<5%)";

        Console.WriteLine($"{nome,-28}{Formatar(antes),14}{Formatar(depois),14}{variacao,11:F1}%  {leitura}");
    }

    private static string Formatar(double valor) => double.IsNaN(valor) ? "n/d" : valor.ToString("F3");
}
