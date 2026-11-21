// Compara a consulta normal x compilada separando tempo de banco e overhead do cliente.
//   Tempo de banco  = soma das durações dos comandos (ContadorComandosSql)
//   Overhead cliente = tempo total - tempo de banco (tradução/cache da expressão, parâmetros, materialização)
// Uso: RELATORIO003_MEDICAO_CONNECTION="Host=...;Database=relatorio003_medicao;Username=...;Password=..." \
//      dotnet run -c Release --project Relatorio003.Medicao -- [iteracoes=300] [pedidos=20000]
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Relatorio003.Consultas;
using Relatorio003.Dados;
using Relatorio003.Diagnostico;
using Relatorio003.Dominio;

var conexao = Environment.GetEnvironmentVariable("RELATORIO003_MEDICAO_CONNECTION")
    ?? throw new InvalidOperationException("Defina RELATORIO003_MEDICAO_CONNECTION.");
var iteracoes = args.Length > 0 ? int.Parse(args[0]) : 300;
var pedidos = args.Length > 1 ? int.Parse(args[1]) : 20_000;
const int aquecimento = 50;

var contador = new ContadorComandosSql();
var opcoes = new DbContextOptionsBuilder<PedidosDbContext>().UseNpgsql(conexao).AddInterceptors(contador).Options;

await using (var db = new PedidosDbContext(opcoes))
{
    await db.Database.MigrateAsync();
    await GeradorDeMassa.GerarAsync(db, pedidos);
}

var status = Enum.GetValues<StatusPedido>();

async Task<(double Total, double Banco)> Executar(bool compilada, int i)
{
    var s = status[i % status.Length];
    var pular = (i % 10) * 20;
    contador.Reiniciar();

    var sw = Stopwatch.StartNew();
    await using var db = new PedidosDbContext(opcoes); // um contexto por "requisição"
    var origem = compilada
        ? PedidoConsultas.PorStatusCompilada(db, s, pular, 20)
        : PedidoConsultas.PorStatus(db, s, pular, 20).AsAsyncEnumerable();
    var n = 0;
    await foreach (var _ in origem) n++;
    sw.Stop();

    return (sw.Elapsed.TotalMilliseconds, contador.DuracaoTotal.TotalMilliseconds);
}

for (var i = 0; i < aquecimento; i++) { await Executar(false, i); await Executar(true, i); }

var normal = new List<(double Total, double Banco)>();
var comp = new List<(double Total, double Banco)>();
for (var i = 0; i < iteracoes; i++)
{
    // Alterna a ordem para não favorecer nenhuma das versões (cache de página do banco, JIT, GC).
    if (i % 2 == 0) { normal.Add(await Executar(false, i)); comp.Add(await Executar(true, i)); }
    else { comp.Add(await Executar(true, i)); normal.Add(await Executar(false, i)); }
}

static double Media(IEnumerable<double> x) => x.Average();
static double Mediana(IEnumerable<double> x) { var o = x.Order().ToList(); return o[o.Count / 2]; }

Console.WriteLine($"Pedidos: {pedidos} | iterações: {iteracoes} (após {aquecimento} de aquecimento)");
Console.WriteLine($"{"Versão",-10} {"Total med.",11} {"Banco med.",11} {"Overhead med.",14} {"Overhead mediana",17}  (ms)");
void Linha(string nome, List<(double Total, double Banco)> d) =>
    Console.WriteLine($"{nome,-10} {Media(d.Select(x => x.Total)),11:F3} {Media(d.Select(x => x.Banco)),11:F3} " +
                      $"{Media(d.Select(x => x.Total - x.Banco)),14:F3} {Mediana(d.Select(x => x.Total - x.Banco)),17:F3}");
Linha("Normal", normal);
Linha("Compilada", comp);

var ganho = Media(normal.Select(x => x.Total - x.Banco)) - Media(comp.Select(x => x.Total - x.Banco));
Console.WriteLine($"Ganho médio de overhead do cliente com a consulta compilada: {ganho:F3} ms/consulta");
