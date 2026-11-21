using Microsoft.EntityFrameworkCore;
using Relatorio003.Dominio;
using Relatorio003.Servicos;

namespace Relatorio003.Tests;

/// <summary>Todos os testes que escrevem usam transação descartada (rollback), sem afetar os demais.</summary>
[Collection(PostgresCollection.Nome)]
public class TrackingTests(PostgresFixture fx)
{
    private int IdComStatus(StatusPedido status) => fx.Todos.First(p => p.Status == status).Id;

    [Fact]
    public async Task Consulta_com_tracking_rastreia_e_SaveChanges_atualiza_so_a_coluna_alterada()
    {
        await using var t = fx.CriarContexto();
        await using var tx = await t.Db.Database.BeginTransactionAsync();
        var id = IdComStatus(StatusPedido.Novo);

        var pedido = await t.Db.Pedidos.FirstAsync(p => p.Id == id); // tracking (padrão)
        Assert.Equal(EntityState.Unchanged, t.Db.Entry(pedido).State);

        pedido.Status = StatusPedido.Pago;
        t.Contador.Reiniciar();
        await t.Db.SaveChangesAsync();

        var update = t.Contador.Comandos.Single().Sql;
        Assert.StartsWith("UPDATE", update.TrimStart());
        Assert.Contains("\"Status\"", update);
        Assert.DoesNotContain("\"Cliente\"", update);

        t.Db.ChangeTracker.Clear();
        Assert.Equal(StatusPedido.Pago, (await t.Db.Pedidos.AsNoTracking().FirstAsync(p => p.Id == id)).Status);
    }

    [Fact]
    public async Task Servico_de_comando_usa_tracking_no_mesmo_contexto()
    {
        await using var t = fx.CriarContexto();
        await using var tx = await t.Db.Database.BeginTransactionAsync();
        var id = IdComStatus(StatusPedido.Novo);

        Assert.True(await new PedidoComandoService(t.Db).AtualizarStatusAsync(id, StatusPedido.Enviado));

        t.Contador.GarantirNoMaximo(2); // SELECT + UPDATE
        t.Db.ChangeTracker.Clear();
        Assert.Equal(StatusPedido.Enviado, (await t.Db.Pedidos.AsNoTracking().FirstAsync(p => p.Id == id)).Status);
    }

    [Fact]
    public async Task NoTracking_sem_anexar_NAO_persiste_alteracoes()
    {
        await using var t = fx.CriarContexto();
        await using var tx = await t.Db.Database.BeginTransactionAsync();
        var id = IdComStatus(StatusPedido.Novo);

        var pedido = await t.Db.Pedidos.AsNoTracking().FirstAsync(p => p.Id == id);
        Assert.Equal(EntityState.Detached, t.Db.Entry(pedido).State);

        pedido.Status = StatusPedido.Cancelado;
        Assert.Equal(0, await t.Db.SaveChangesAsync()); // nada rastreado => nada salvo

        Assert.Equal(StatusPedido.Novo, (await t.Db.Pedidos.AsNoTracking().FirstAsync(p => p.Id == id)).Status);
    }

    [Fact]
    public async Task NoTracking_com_anexacao_explicita_atualiza_somente_Status_com_1_comando()
    {
        await using var t = fx.CriarContexto();
        await using var tx = await t.Db.Database.BeginTransactionAsync();
        var id = IdComStatus(StatusPedido.Novo);
        var clienteAntes = fx.Todos.First(p => p.Id == id).Cliente;

        t.Contador.Reiniciar();
        Assert.True(await new PedidoComandoService(t.Db).AtualizarStatusAnexandoAsync(id, StatusPedido.Entregue));

        Assert.Equal(1, t.Contador.Total);
        var update = t.Contador.Comandos.Single().Sql;
        Assert.Contains("\"Status\"", update);
        Assert.DoesNotContain("\"Cliente\"", update);

        t.Db.ChangeTracker.Clear();
        var depois = await t.Db.Pedidos.AsNoTracking().FirstAsync(p => p.Id == id);
        Assert.Equal(StatusPedido.Entregue, depois.Status);
        Assert.Equal(clienteAntes, depois.Cliente); // colunas não carregadas não foram sobrescritas
    }

    [Fact]
    public async Task Anexacao_de_pedido_inexistente_retorna_false()
    {
        await using var t = fx.CriarContexto();
        await using var tx = await t.Db.Database.BeginTransactionAsync();

        Assert.False(await new PedidoComandoService(t.Db).AtualizarStatusAnexandoAsync(int.MaxValue, StatusPedido.Pago));
        Assert.Empty(t.Db.ChangeTracker.Entries());
    }
}
