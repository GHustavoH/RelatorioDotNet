using System.Text.Json;
using System.Text.Json.Serialization;

namespace Relatorio005.LoadTest;

public enum Rota
{
    Criacao,
    Consulta
}

public enum Fase
{
    Aquecimento,
    Medicao
}

public enum Resultado
{
    Sucesso,
    RejeitadaServidor,
    ErroHttp,
    Timeout,
    ErroConexao,
    DescartadaGerador
}

public readonly record struct Amostra(double OffsetS, Rota Rota, Fase Fase, Resultado Resultado, int Status, double LatenciaMs);

public sealed record Opcoes(
    string Url,
    double Taxa,
    int AquecimentoS,
    int MedicaoS,
    int TimeoutMs,
    int MaxEmVoo,
    double MixCriacao,
    int Semente,
    string Rotulo,
    string Saida);

public sealed record ResumoRota(
    int Emitidas,
    int Sucessos,
    int Rejeicoes,
    int ErrosHttp,
    int Timeouts,
    int ErrosConexao,
    int DescartadasGerador,
    double TaxaErro,
    double VazaoRps,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    double MaxMs,
    double MediaMs,
    double P95TodasMs,
    double P99TodasMs);

public sealed record MetaItem(string Nome, double Medido, double Limite, string Comparacao, bool Atingida);

public sealed record AmbienteInfo(
    string SistemaOperacional,
    string Arquitetura,
    string Processador,
    int NucleosLogicos,
    long MemoriaTotalMb,
    string RuntimeDotNet,
    string SdkDotNet,
    string CompilacaoGerador,
    DateTime ColetadoEmUtc);

public sealed record ResultadoExecucao(
    string Rotulo,
    DateTime InicioUtc,
    Opcoes Parametros,
    AmbienteInfo Ambiente,
    JsonElement? ServidorAntes,
    JsonElement? ServidorDepois,
    ResumoRota Geral,
    ResumoRota Criacao,
    ResumoRota Consulta,
    IReadOnlyList<MetaItem> Metas,
    string ArquivoAmostras);

public sealed class ExperimentoConfig
{
    public MetasConfig Metas { get; set; } = new();
    public CargaConfig Carga { get; set; } = new();
    public Dictionary<string, JsonElement> Servidor { get; set; } = new();
}

public sealed class MetasConfig
{
    public MetaRota Consulta { get; set; } = new();
    public MetaRota Criacao { get; set; } = new();
    public double VazaoMinimaRelativa { get; set; } = 0.95;
    public double TaxaErroMaxima { get; set; } = 0.01;
}

public sealed class MetaRota
{
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
}

public sealed class CargaConfig
{
    public string Url { get; set; } = "http://localhost:5249";
    public double Taxa { get; set; } = 150;
    public int AquecimentoS { get; set; } = 15;
    public int MedicaoS { get; set; } = 60;
    public int TimeoutMs { get; set; } = 2000;
    public int MaxEmVoo { get; set; } = 512;
    public double MixCriacao { get; set; } = 0.3;
    public int Semente { get; set; } = 123;
}

public static class ConfiguracaoJson
{
    public static readonly JsonSerializerOptions Serializacao = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() }
    };
}
