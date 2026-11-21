using Microsoft.EntityFrameworkCore;
using Npgsql;
using Relatorio003.Dados;
using Relatorio003.Diagnostico;
using Relatorio003.Dominio;

namespace Relatorio003.Tests;

[CollectionDefinition(Nome)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Nome = "postgres";
}

public sealed class TestContexto(PedidosDbContext db, ContadorComandosSql contador) : IAsyncDisposable
{
    public PedidosDbContext Db { get; } = db;
    public ContadorComandosSql Contador { get; } = contador;
    public ValueTask DisposeAsync() => Db.DisposeAsync();
}

/// <summary>
/// Cria um banco PostgreSQL exclusivo para a execução, aplica as MIGRATIONS (valida o esquema reproduzível)
/// e gera a massa de dados. Requer a variável RELATORIO003_TEST_CONNECTION (usuário com permissão CREATEDB).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const int TotalPedidos = 500;
    public const string VariavelConexao = "RELATORIO003_TEST_CONNECTION";

    public string ConnectionString { get; private set; } = "";
    public IReadOnlyList<Pedido> Todos { get; private set; } = [];

    public async Task InitializeAsync()
    {
        var admin = Environment.GetEnvironmentVariable(VariavelConexao)
            ?? throw new InvalidOperationException(
                $"Defina {VariavelConexao} (ex.: Host=localhost;Username=postgres;Password=<senha>). Veja o LEIAME.md.");

        ConnectionString = new NpgsqlConnectionStringBuilder(admin)
        {
            Database = $"relatorio003_test_{Guid.NewGuid():N}"
        }.ConnectionString;

        await using var t = CriarContexto();

        if (!t.Db.Database.GetMigrations().Any())
            throw new InvalidOperationException(
                "Nenhuma migration encontrada. Gere com: dotnet ef migrations add Inicial --project Relatorio003");

        await t.Db.Database.MigrateAsync();
        await GeradorDeMassa.GerarAsync(t.Db, TotalPedidos);

        Todos = await t.Db.Pedidos.AsNoTracking().Include(p => p.Itens).ToListAsync();
    }

    public async Task DisposeAsync()
    {
        await using var t = CriarContexto();
        NpgsqlConnection.ClearAllPools();
        await t.Db.Database.EnsureDeletedAsync();
    }

    public TestContexto CriarContexto()
    {
        var contador = new ContadorComandosSql();
        var opcoes = new DbContextOptionsBuilder<PedidosDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(contador)
            .Options;
        return new TestContexto(new PedidosDbContext(opcoes), contador);
    }
}
