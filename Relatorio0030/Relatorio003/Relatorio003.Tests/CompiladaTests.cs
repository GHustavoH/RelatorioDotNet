using Microsoft.EntityFrameworkCore;
using Relatorio003.Consultas;
using Relatorio003.Dominio;

namespace Relatorio003.Tests;

[Collection(PostgresCollection.Nome)]
public class CompiladaTests(PostgresFixture fx)
{
    private static async Task<List<T>> Materializar<T>(IAsyncEnumerable<T> origem)
    {
        var lista = new List<T>();
        await foreach (var x in origem) lista.Add(x);
        return lista;
    }

    [Theory]
    [InlineData(StatusPedido.Pago, 0, 10)]
    [InlineData(StatusPedido.Novo, 10, 25)]
    public async Task Versao_compilada_retorna_o_mesmo_resultado_e_o_mesmo_SQL(StatusPedido status, int pular, int tamanho)
    {
        await using var normal = fx.CriarContexto();
        await using var compilada = fx.CriarContexto();

        var a = await Materializar(PedidoConsultas.PorStatus(normal.Db, status, pular, tamanho).AsAsyncEnumerable());
        var b = await Materializar(PedidoConsultas.PorStatusCompilada(compilada.Db, status, pular, tamanho));

        Assert.Equal(a, b); // records: igualdade por valor

        Assert.Equal(1, normal.Contador.Total);
        Assert.Equal(1, compilada.Contador.Total);
        Assert.Equal(
            SqlTestes.Normalizar(normal.Contador.Comandos[0].Sql),
            SqlTestes.Normalizar(compilada.Contador.Comandos[0].Sql));
    }

    [Fact]
    public async Task Versao_compilada_nao_embute_valores_como_constantes()
    {
        await using var t = fx.CriarContexto();

        await Materializar(PedidoConsultas.PorStatusCompilada(t.Db, StatusPedido.Pago, 0, 10));
        await Materializar(PedidoConsultas.PorStatusCompilada(t.Db, StatusPedido.Enviado, 30, 5));

        var sqls = t.Contador.Comandos.Select(c => SqlTestes.Normalizar(c.Sql)).ToList();
        Assert.Equal(2, sqls.Count);
        Assert.Equal(sqls[0], sqls[1]);
        Assert.All(sqls, s => { Assert.DoesNotContain("Pago", s); Assert.DoesNotContain("Enviado", s); });
    }

    [Fact]
    public async Task Versao_compilada_exige_apenas_um_comando_por_execucao_e_nao_rastreia()
    {
        await using var t = fx.CriarContexto();
        await Materializar(PedidoConsultas.PorStatusCompilada(t.Db, StatusPedido.Pago, 0, 20));

        t.Contador.GarantirNoMaximo(1);
        Assert.Empty(t.Db.ChangeTracker.Entries());
    }
}
