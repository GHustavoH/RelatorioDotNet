using Microsoft.EntityFrameworkCore;
using Relatorio003.Consultas;
using Relatorio003.Dados;
using Relatorio003.Diagnostico;
using Relatorio003.Servicos;

namespace Relatorio003.Tests;

[Collection(PostgresCollection.Nome)]
public class RelatorioNMaisUmTests(PostgresFixture fx)
{
    private const int Tamanho = 20;

    [Theory]
    [InlineData(EstrategiaRelatorio.Projecao, 1)]
    [InlineData(EstrategiaRelatorio.IncludeUnico, 1)]
    [InlineData(EstrategiaRelatorio.IncludeDividido, 2)]
    public async Task Relatorio_nao_tem_N_mais_1_e_respeita_o_orcamento_de_comandos(EstrategiaRelatorio estrategia, int maximo)
    {
        await using var t = fx.CriarContexto();
        var svc = new PedidoRelatorioService(t.Db);

        var r = await svc.ObterPaginaAsync(estrategia, 1, Tamanho);

        Assert.Equal(Tamanho, r.Count);
        Assert.All(r, p => Assert.NotEmpty(p.Itens));
        // Falha se o número de comandos crescer com o tamanho da página (N+1) ou passar do orçamento.
        t.Contador.GarantirNoMaximo(maximo);
        Assert.Empty(t.Db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Estrategias_retornam_os_mesmos_dados()
    {
        await using var t = fx.CriarContexto();
        var svc = new PedidoRelatorioService(t.Db);

        var a = Assinatura(await svc.ObterPaginaAsync(EstrategiaRelatorio.Projecao, 2, Tamanho));
        var b = Assinatura(await svc.ObterPaginaAsync(EstrategiaRelatorio.IncludeUnico, 2, Tamanho));
        var c = Assinatura(await svc.ObterPaginaAsync(EstrategiaRelatorio.IncludeDividido, 2, Tamanho));

        Assert.Equal(a, b);
        Assert.Equal(a, c);
    }

    [Fact]
    public async Task Guarda_detecta_N_mais_1_em_uma_implementacao_ingenua()
    {
        await using var t = fx.CriarContexto();

        var r = await RelatorioIngenuoAsync(t.Db, Tamanho);

        Assert.Equal(Tamanho, r.Count);
        Assert.Equal(Tamanho + 1, t.Contador.Total); // 1 consulta de pedidos + 1 por pedido
        var ex = Assert.Throws<LimiteDeComandosExcedidoException>(() => t.Contador.GarantirNoMaximo(2));
        Assert.Contains($"{Tamanho + 1} executados", ex.Message);
    }

    /// <summary>Anti-padrão (N+1) mantido apenas aqui, para provar que a guarda o detecta.</summary>
    private static async Task<List<PedidoRelatorioDto>> RelatorioIngenuoAsync(PedidosDbContext db, int tamanho)
    {
        var pedidos = await db.Pedidos.AsNoTracking()
            .OrderByDescending(p => p.CriadoEm).ThenByDescending(p => p.Id)
            .Take(tamanho).ToListAsync();

        var lista = new List<PedidoRelatorioDto>();
        foreach (var p in pedidos)
        {
            var itens = await db.Itens.AsNoTracking().Where(i => i.PedidoId == p.Id).OrderBy(i => i.Id).ToListAsync();
            lista.Add(new PedidoRelatorioDto(p.Id, p.Numero, p.Cliente, p.Status, p.Total,
                itens.Select(i => new ItemRelatorioDto(i.Produto, i.Quantidade, i.PrecoUnitario, i.Quantidade * i.PrecoUnitario)).ToList()));
        }
        return lista;
    }

    private static List<string> Assinatura(List<PedidoRelatorioDto> lista) =>
        lista.Select(p => $"{p.Id}|{p.Numero}|{p.Total}|" +
                          string.Join(",", p.Itens.Select(i => $"{i.Produto}x{i.Quantidade}@{i.PrecoUnitario}"))).ToList();
}
