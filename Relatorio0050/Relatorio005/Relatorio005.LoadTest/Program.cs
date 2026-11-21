using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Relatorio005.LoadTest;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

if (args.Length >= 3 && args[0] == "comparar")
{
    return Comparador.Executar(args[1], args[2]);
}

var argumentos = Argumentos.Ler(args);
var experimento = CarregarExperimento(argumentos.Texto("experimento"));

if (args.Length >= 2 && args[0] == "variaveis")
{
    var exp = CarregarExperimento(args[1]);
    foreach (var (chave, valor) in exp.Servidor)
    {
        Console.WriteLine($"Hub__{chave}={valor}");
    }

    Console.WriteLine($"LT_URL={exp.Carga.Url}");
    Console.WriteLine($"LT_AQUECIMENTO_S={exp.Carga.AquecimentoS}");
    Console.WriteLine($"LT_MEDICAO_S={exp.Carga.MedicaoS}");
    return 0;
}

var carga = experimento.Carga;
var opcoes = new Opcoes(
    argumentos.Texto("url", carga.Url),
    argumentos.Real("taxa", carga.Taxa),
    argumentos.Inteiro("aquecimento", carga.AquecimentoS),
    argumentos.Inteiro("medicao", carga.MedicaoS),
    argumentos.Inteiro("timeoutMs", carga.TimeoutMs),
    argumentos.Inteiro("maxEmVoo", carga.MaxEmVoo),
    argumentos.Real("mixCriacao", carga.MixCriacao),
    argumentos.Inteiro("semente", carga.Semente),
    argumentos.Texto("rotulo", "execucao"),
    argumentos.Texto("saida", "resultados"));

if (opcoes.Taxa <= 0 || opcoes.MedicaoS <= 0 || opcoes.AquecimentoS < 0 || opcoes.MixCriacao is < 0 or > 1)
{
    Console.Error.WriteLine("Parametros invalidos: taxa > 0, medicao > 0, aquecimento >= 0 e mixCriacao entre 0 e 1.");
    return 1;
}

Directory.CreateDirectory(opcoes.Saida);

using var cancelamento = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancelamento.Cancel();
};

using var http = new HttpClient { BaseAddress = new Uri(opcoes.Url), Timeout = TimeSpan.FromSeconds(5) };

var servidorAntes = await LerEstadoAsync(http);
if (servidorAntes is null)
{
    Console.Error.WriteLine($"Servidor indisponivel em {opcoes.Url}.");
    return 1;
}

var ambiente = Ambiente.Coletar();
var inicioUtc = DateTime.UtcNow;

Console.WriteLine($"Rotulo: {opcoes.Rotulo}");
Console.WriteLine($"Carga : {opcoes.Taxa} req/s, aquecimento {opcoes.AquecimentoS}s, medicao {opcoes.MedicaoS}s, timeout {opcoes.TimeoutMs}ms, max em voo {opcoes.MaxEmVoo}, criacao {opcoes.MixCriacao:P0}");
Console.WriteLine($"Ambiente: {ambiente.SistemaOperacional} | {ambiente.Processador} | {ambiente.NucleosLogicos} nucleos | {ambiente.MemoriaTotalMb} MB | {ambiente.RuntimeDotNet} | SDK {ambiente.SdkDotNet}");
Console.WriteLine($"Servidor: {servidorAntes}");
Console.WriteLine("Executando...");

var amostras = await new GeradorCarga().ExecutarAsync(opcoes, cancelamento.Token);
var servidorDepois = await LerEstadoAsync(http);

var medicao = amostras.Where(a => a.Fase == Fase.Medicao).ToArray();
var geral = Estatisticas.Resumir(medicao, opcoes.MedicaoS);
var criacao = Estatisticas.Resumir(medicao.Where(a => a.Rota == Rota.Criacao).ToArray(), opcoes.MedicaoS);
var consulta = Estatisticas.Resumir(medicao.Where(a => a.Rota == Rota.Consulta).ToArray(), opcoes.MedicaoS);
var metas = Estatisticas.AvaliarMetas(experimento.Metas, opcoes.Taxa, geral, criacao, consulta);

var arquivoAmostras = Path.Combine(opcoes.Saida, $"{opcoes.Rotulo}-amostras.csv");
EscreverAmostras(arquivoAmostras, amostras);

var resultado = new ResultadoExecucao(
    opcoes.Rotulo,
    inicioUtc,
    opcoes,
    ambiente,
    servidorAntes,
    servidorDepois,
    geral,
    criacao,
    consulta,
    metas,
    arquivoAmostras);

var arquivoResultado = Path.Combine(opcoes.Saida, $"{opcoes.Rotulo}.json");
await File.WriteAllTextAsync(arquivoResultado, JsonSerializer.Serialize(resultado, ConfiguracaoJson.Serializacao));

Console.WriteLine();
Console.WriteLine($"Amostras: {amostras.Count} total, {amostras.Count - medicao.Length} no aquecimento (descartadas das estatisticas), {medicao.Length} na janela de medicao");
Console.WriteLine($"{"Rota",-10}{"Emitidas",9}{"Sucesso",9}{"503/429",9}{"ErrHttp",9}{"Timeout",9}{"Conexao",9}{"Descart.",9}{"Erro%",8}{"req/s",9}{"p50",9}{"p95",9}{"p99",9}{"max",9}");
Imprimir("Criacao", criacao);
Imprimir("Consulta", consulta);
Imprimir("Geral", geral);

Console.WriteLine();
foreach (var meta in metas)
{
    Console.WriteLine($"[{(meta.Atingida ? "OK" : "FALHOU")}] {meta.Nome}: medido {meta.Medido:F3} {meta.Comparacao} {meta.Limite:F3}");
}

Console.WriteLine();
Console.WriteLine($"Resultado: {arquivoResultado}");
Console.WriteLine($"Amostras : {arquivoAmostras}");
return 0;

static ExperimentoConfig CarregarExperimento(string? caminho)
{
    if (string.IsNullOrWhiteSpace(caminho) || !File.Exists(caminho))
    {
        return new ExperimentoConfig();
    }

    return JsonSerializer.Deserialize<ExperimentoConfig>(File.ReadAllText(caminho), ConfiguracaoJson.Serializacao)
        ?? new ExperimentoConfig();
}

static async Task<JsonElement?> LerEstadoAsync(HttpClient http)
{
    try
    {
        return await http.GetFromJsonAsync<JsonElement>("/api/diagnostico/estado");
    }
    catch (Exception)
    {
        return null;
    }
}

static void EscreverAmostras(string arquivo, IReadOnlyList<Amostra> amostras)
{
    using var escritor = new StreamWriter(arquivo);
    escritor.WriteLine("offset_s,fase,rota,resultado,status_http,latencia_ms");

    foreach (var a in amostras.OrderBy(x => x.OffsetS))
    {
        escritor.WriteLine(string.Join(',',
            a.OffsetS.ToString("F3", CultureInfo.InvariantCulture),
            a.Fase,
            a.Rota,
            a.Resultado,
            a.Status,
            a.LatenciaMs.ToString("F3", CultureInfo.InvariantCulture)));
    }
}

static void Imprimir(string nome, ResumoRota r)
{
    Console.WriteLine(
        $"{nome,-10}{r.Emitidas,9}{r.Sucessos,9}{r.Rejeicoes,9}{r.ErrosHttp,9}{r.Timeouts,9}{r.ErrosConexao,9}{r.DescartadasGerador,9}" +
        $"{r.TaxaErro * 100,8:F2}{r.VazaoRps,9:F1}{F(r.P50Ms),9}{F(r.P95Ms),9}{F(r.P99Ms),9}{F(r.MaxMs),9}");
}

static string F(double valor) => double.IsNaN(valor) ? "n/d" : valor.ToString("F2", CultureInfo.InvariantCulture);
