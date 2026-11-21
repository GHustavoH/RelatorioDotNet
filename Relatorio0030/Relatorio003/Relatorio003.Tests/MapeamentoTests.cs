using Microsoft.EntityFrameworkCore;
using Relatorio003.Dominio;

namespace Relatorio003.Tests;

/// <summary>Relacional (EnderecoEntrega: colunas) x JSON (Metadados: jsonb). SQL impresso como evidência.</summary>
[Collection(PostgresCollection.Nome)]
public class MapeamentoTests(PostgresFixture fx, ITestOutputHelper saida)
{
    [Fact]
    public async Task Provedor_e_versoes_registrados()
    {
        await using var t = fx.CriarContexto();
        var ef = typeof(DbContext).Assembly.GetName().Version;
        var npgsql = typeof(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder).Assembly.GetName().Version;
        saida.WriteLine($"Provedor: {t.Db.Database.ProviderName} | EF Core {ef} | Npgsql EF {npgsql}");

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", t.Db.Database.ProviderName);
    }

    [Fact]
    public async Task Esquema_tem_endereco_em_colunas_e_metadados_em_jsonb()
    {
        await using var t = fx.CriarContexto();

        var colunas = await t.Db.Database
            .SqlQuery<string>($"SELECT column_name || ':' || data_type AS \"Value\" FROM information_schema.columns WHERE table_name = 'Pedidos'")
            .ToListAsync();

        Assert.Contains("EnderecoEntrega_Cidade:character varying", colunas);
        Assert.Contains("Metadados:jsonb", colunas);
    }

    [Fact]
    public async Task Filtro_relacional_usa_coluna_e_bate_com_o_calculo_em_memoria()
    {
        await using var t = fx.CriarContexto();
        var cidade = "Campinas";

        var consulta = t.Db.Pedidos.AsNoTracking().Where(p => p.EnderecoEntrega.Cidade == cidade);
        saida.WriteLine(consulta.ToQueryString());
        var total = await consulta.CountAsync();

        Assert.Equal(fx.Todos.Count(p => p.EnderecoEntrega.Cidade == cidade), total);
        Assert.Contains("EnderecoEntrega_Cidade", t.Contador.Comandos.Single().Sql);
    }

    [JsonSuportadoFact]
    public async Task Filtro_em_JSON_e_traduzido_para_jsonb_e_bate_com_o_calculo_em_memoria()
    {
        await using var t = fx.CriarContexto();
        var canal = "app";

        var consulta = t.Db.Pedidos.AsNoTracking().Where(p => p.Metadados.Canal == canal);
        saida.WriteLine(consulta.ToQueryString());
        var total = await consulta.CountAsync();

        Assert.Equal(fx.Todos.Count(p => p.Metadados.Canal == canal), total);
        Assert.Contains("Metadados", t.Contador.Comandos.Single().Sql);
    }

    [JsonSuportadoFact]
    public async Task JSON_grava_e_le_o_agregado_completo()
    {
        await using var t = fx.CriarContexto();
        await using var tx = await t.Db.Database.BeginTransactionAsync();

        var pedido = new Pedido
        {
            Numero = "TESTE-JSON",
            Cliente = "Cliente JSON",
            Status = StatusPedido.Novo,
            CriadoEm = DateTimeOffset.UtcNow,
            EnderecoEntrega = new Endereco { Logradouro = "Rua A, 1", Cidade = "Artur Nogueira", Uf = "SP", Cep = "13160000" },
            Metadados = new MetadadosPedido { Canal = "app", Cupom = "PROMO10", Tags = ["promo", "presente"] }
        };
        t.Db.Pedidos.Add(pedido);
        await t.Db.SaveChangesAsync();

        t.Db.ChangeTracker.Clear();
        var lido = await t.Db.Pedidos.AsNoTracking().SingleAsync(p => p.Numero == "TESTE-JSON");

        Assert.Equal("app", lido.Metadados.Canal);
        Assert.Equal("PROMO10", lido.Metadados.Cupom);
        Assert.Equal(["promo", "presente"], lido.Metadados.Tags);
    }

    [JsonSuportadoFact]
    public async Task JSON_com_tracking_detecta_alteracao_interna_e_atualiza()
    {
        await using var t = fx.CriarContexto();
        await using var tx = await t.Db.Database.BeginTransactionAsync();
        var id = fx.Todos.First(p => p.Metadados.Cupom == null).Id;

        var pedido = await t.Db.Pedidos.SingleAsync(p => p.Id == id);
        pedido.Metadados.Cupom = "NOVO10";
        t.Contador.Reiniciar();
        await t.Db.SaveChangesAsync();

        var update = t.Contador.Comandos.Single().Sql;
        saida.WriteLine(update);
        Assert.Contains("Metadados", update);

        t.Db.ChangeTracker.Clear();
        Assert.Equal("NOVO10", (await t.Db.Pedidos.AsNoTracking().SingleAsync(p => p.Id == id)).Metadados.Cupom);
    }

    [JsonSuportadoFact]
    public async Task ExecuteUpdate_em_JSON_altera_o_valor_dentro_do_jsonb()
    {
        await using var t = fx.CriarContexto();
        await using var tx = await t.Db.Database.BeginTransactionAsync();
        var id = fx.Todos.First(p => p.Metadados.Canal != "balcao").Id;

        var linhas = await t.Db.Pedidos.Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Metadados.Canal, "balcao"));

        Assert.Equal(1, linhas);
        saida.WriteLine(t.Contador.Comandos.Last().Sql);
        Assert.Contains("Metadados", t.Contador.Comandos.Last().Sql);
        Assert.Equal("balcao", (await t.Db.Pedidos.AsNoTracking().SingleAsync(p => p.Id == id)).Metadados.Canal);
    }

    [Fact]
    public async Task ExecuteUpdate_relacional_altera_somente_a_coluna_da_cidade()
    {
        await using var t = fx.CriarContexto();
        await using var tx = await t.Db.Database.BeginTransactionAsync();
        var id = fx.Todos.First().Id;

        var linhas = await t.Db.Pedidos.Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.EnderecoEntrega.Cidade, "Americana"));

        Assert.Equal(1, linhas);
        var sql = t.Contador.Comandos.Last().Sql;
        saida.WriteLine(sql);
        Assert.Contains("\"EnderecoEntrega_Cidade\"", sql);
        Assert.DoesNotContain("Metadados", sql);
    }
}
