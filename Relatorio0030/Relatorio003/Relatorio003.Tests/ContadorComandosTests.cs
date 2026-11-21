using Microsoft.EntityFrameworkCore;
using Relatorio003.Diagnostico;

namespace Relatorio003.Tests;

[Collection(PostgresCollection.Nome)]
public class ContadorComandosTests(PostgresFixture fx)
{
    [Fact]
    public async Task Guarda_registra_texto_e_duracao_dos_comandos()
    {
        await using var t = fx.CriarContexto();
        await t.Db.Pedidos.AsNoTracking().CountAsync();

        Assert.Equal(1, t.Contador.Total);
        Assert.Contains("Pedidos", t.Contador.Comandos[0].Sql);
    }

    [Fact]
    public async Task Guarda_FALHA_quando_a_quantidade_de_comandos_excede_o_limite()
    {
        await using var t = fx.CriarContexto();

        // Três consultas = 3 comandos; limite 2 => deve lançar.
        await t.Db.Pedidos.AsNoTracking().CountAsync();
        await t.Db.Itens.AsNoTracking().CountAsync();
        await t.Db.Pedidos.AsNoTracking().AnyAsync();

        var ex = Assert.Throws<LimiteDeComandosExcedidoException>(() => t.Contador.GarantirNoMaximo(2));
        Assert.Contains("3 executados", ex.Message);
    }

    [Fact]
    public async Task Guarda_nao_falha_dentro_do_limite()
    {
        await using var t = fx.CriarContexto();
        await t.Db.Pedidos.AsNoTracking().CountAsync();
        t.Contador.GarantirNoMaximo(1);
    }
}
