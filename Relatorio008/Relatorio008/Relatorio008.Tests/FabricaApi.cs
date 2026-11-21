
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;


namespace Relatorio008.Tests;

public class FabricaApi : WebApplicationFactory<Program>
{
    public const string ChaveApi = "chave-de-teste";

    private readonly SqliteConnection _conexao = new("DataSource=:memory:");

    public FilaFalsa Fila { get; } = new();

    public FabricaApi()
    {
        _conexao.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKey"] = ChaveApi
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_conexao));
            services.AddSingleton<IFilaPedidos>(Fila);
        });
    }

    // cria o esquema e os dados minimos (pedido 1)
    public void PrepararBanco()
    {
        ExecutarNoBanco(db =>
        {
            db.Database.EnsureCreated();
            db.Pedidos.Add(new Pedido
            {
                Cliente = "Cliente Seed",
                Valor = 10m,
                CriadoEm = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
            });
            db.SaveChanges();
        });
    }

    public void ExecutarNoBanco(Action<AppDbContext> acao)
    {
        using var escopo = Services.CreateScope();
        acao(escopo.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public HttpClient CriarCliente(bool comChave = true)
    {
        var cliente = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        if (comChave)
        {
            cliente.DefaultRequestHeaders.Add("X-Api-Key", ChaveApi);
        }

        return cliente;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _conexao.Dispose();
        }
    }
}
