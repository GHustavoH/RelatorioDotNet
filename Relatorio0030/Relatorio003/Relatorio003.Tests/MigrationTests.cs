using Microsoft.EntityFrameworkCore;

namespace Relatorio003.Tests;

[Collection(PostgresCollection.Nome)]
public class MigrationTests(PostgresFixture fx)
{
    [Fact]
    public async Task Esquema_e_reproduzivel_pelas_migrations_sem_diferencas_pendentes()
    {
        await using var t = fx.CriarContexto();

        Assert.NotEmpty(t.Db.Database.GetMigrations());
        Assert.Empty(await t.Db.Database.GetPendingMigrationsAsync());
        Assert.False(t.Db.Database.HasPendingModelChanges(),
            "O modelo mudou depois da última migration. Gere outra: dotnet ef migrations add <Nome>");
    }

    [Fact]
    public async Task Indices_e_nulabilidade_revisados_existem_no_banco()
    {
        await using var t = fx.CriarContexto();

        var indices = await t.Db.Database
            .SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE tablename IN ('Pedidos','ItensPedido')")
            .ToListAsync();
        Assert.Contains("IX_Pedidos_Numero", indices);
        Assert.Contains("IX_Pedidos_CriadoEm_Id", indices);
        Assert.Contains("IX_Pedidos_Status_CriadoEm_Id", indices);
        Assert.Contains("IX_ItensPedido_PedidoId", indices);

        var obrigatorias = await t.Db.Database
            .SqlQuery<string>($"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = 'Pedidos' AND is_nullable = 'NO'")
            .ToListAsync();
        foreach (var coluna in new[] { "Numero", "Cliente", "Status", "CriadoEm", "Total", "EnderecoEntrega_Cidade", "Metadados" })
            Assert.Contains(coluna, obrigatorias);
    }
}
