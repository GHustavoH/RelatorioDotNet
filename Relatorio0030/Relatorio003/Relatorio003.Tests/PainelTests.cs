using Relatorio003.Consultas;
using Relatorio003.Dominio;
using Relatorio003.Servicos;
using Microsoft.EntityFrameworkCore;

namespace Relatorio003.Tests;

[Collection(PostgresCollection.Nome)]
public class PainelTests(PostgresFixture fx, ITestOutputHelper saida)
{
    [Fact]
    public async Task Pagina_retorna_somente_linhas_e_colunas_necessarias()
    {
        await using var t = fx.CriarContexto();
        var svc = new PedidoPainelService(t.Db);
        t.Contador.Reiniciar();

        var r = await svc.ListarAsync(new FiltroPedidos(Status: StatusPedido.Pago), pagina: 2, tamanhoPagina: 10);

        Assert.Equal(10, r.Itens.Count);
        Assert.Equal(fx.Todos.Count(p => p.Status == StatusPedido.Pago), r.Total);
        Assert.All(r.Itens, i => Assert.Equal(StatusPedido.Pago, i.Status));

        t.Contador.GarantirNoMaximo(2); // contagem + página
        var select = t.Contador.Comandos.Single(c => c.Sql.Contains("LIMIT")).Sql;
        Assert.Contains("OFFSET", select);
        foreach (var colunaNaoExigida in new[] { "Metadados", "Logradouro", "Cep", "EnderecoEntrega_Uf", "Produto" })
            Assert.DoesNotContain(colunaNaoExigida, select);

        Assert.Empty(t.Db.ChangeTracker.Entries()); // AsNoTracking: nada rastreado
    }

    [Fact]
    public async Task Ordenacao_e_deterministica_sem_duplicar_nem_perder_linhas()
    {
        await using var t = fx.CriarContexto();
        var svc = new PedidoPainelService(t.Db);

        var ids = new List<int>();
        const int tamanho = 37;
        for (var pagina = 1; ; pagina++)
        {
            var r = await svc.ListarAsync(new FiltroPedidos(), pagina, tamanho);
            if (r.Itens.Count == 0) break;
            ids.AddRange(r.Itens.Select(i => i.Id));
        }

        var esperado = fx.Todos.OrderByDescending(p => p.CriadoEm).ThenByDescending(p => p.Id).Select(p => p.Id).ToList();
        Assert.Equal(esperado, ids);
    }

    [Fact]
    public async Task Filtros_combinados_batem_com_o_calculo_em_memoria()
    {
        await using var t = fx.CriarContexto();
        var svc = new PedidoPainelService(t.Db);

        var r = await svc.ListarAsync(
            new FiltroPedidos(StatusPedido.Enviado, "Campinas", "Cliente 1"), 1, 100);

        var esperado = fx.Todos.Count(p => p.Status == StatusPedido.Enviado
            && p.EnderecoEntrega.Cidade == "Campinas"
            && p.Cliente.Contains("Cliente 1", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(esperado, r.Total);
        Assert.Equal(esperado, r.Itens.Count);
    }

    [Fact]
    public async Task Quantidade_de_itens_vem_da_consulta_sem_carregar_itens()
    {
        await using var t = fx.CriarContexto();
        var svc = new PedidoPainelService(t.Db);

        var r = await svc.ListarAsync(new FiltroPedidos(), 1, 25);

        var porId = fx.Todos.ToDictionary(p => p.Id);
        Assert.All(r.Itens, i => Assert.Equal(porId[i.Id].Itens.Count, i.QuantidadeItens));
        t.Contador.GarantirNoMaximo(2);
    }

    [Fact]
    public async Task Evidencia_sql_da_consulta_do_painel()
    {
        await using var t = fx.CriarContexto();
        var svc = new PedidoPainelService(t.Db);

        var consulta = svc.MontarConsultaPagina(new FiltroPedidos(StatusPedido.Pago, "Campinas"), 1, 20);
        saida.WriteLine(consulta.ToQueryString());

        Assert.Equal(0, t.Contador.Total); 
    }
}
