using Relatorio003.Consultas;
using Relatorio003.Dominio;
using Relatorio003.Servicos;

namespace Relatorio003.Tests;

[Collection(PostgresCollection.Nome)]
public class ParametrosTests(PostgresFixture fx)
{
    private async Task<List<string>> ExecutarAsync(FiltroPedidos filtro, int pagina)
    {
        await using var t = fx.CriarContexto();
        await new PedidoPainelService(t.Db).ListarAsync(filtro, pagina, 15);
        return t.Contador.Comandos.Select(c => c.Sql).ToList();
    }

    [Fact]
    public async Task Valores_do_usuario_nao_viram_constantes_nem_mudam_a_forma_do_SQL()
    {
        var a = await ExecutarAsync(new FiltroPedidos(StatusPedido.Pago, "Campinas", "Cliente 1"), pagina: 1);
        var b = await ExecutarAsync(new FiltroPedidos(StatusPedido.Novo, "Curitiba", "Cliente 2"), pagina: 3);

        Assert.Equal(a, b); // mesmo texto SQL, só os parâmetros mudam

        foreach (var sql in a.Concat(b))
        {
            Assert.DoesNotContain("Campinas", sql);
            Assert.DoesNotContain("Curitiba", sql);
            Assert.DoesNotContain("Cliente 1", sql);
            Assert.DoesNotContain("Pago", sql);
        }
    }

    [Fact]
    public async Task Curingas_do_LIKE_digitados_pelo_usuario_sao_tratados_como_texto()
    {
        await using var t = fx.CriarContexto();
        var svc = new PedidoPainelService(t.Db);

        var r = await svc.ListarAsync(new FiltroPedidos(Cliente: "%"), 1, 20);

        Assert.Equal(0, r.Total); // nenhum cliente contém o caractere '%' literal
    }
}
