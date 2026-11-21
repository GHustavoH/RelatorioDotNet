using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RelatorioFinal.Persistencia;

namespace RelatorioFinal.Tests;

public class FabricaApi : WebApplicationFactory<Program>
{
    // banco SQLite em arquivo temporario para cada fabrica assim nunca toca no banco de desenvolvimento
    private readonly string _arquivo = Path.Combine(Path.GetTempPath(), $"hub-teste-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Chave"] = "chave-de-teste-com-mais-de-32-caracteres-aqui",
                ["Fila:AtrasoSimuladoMs"] = "0",
                ["Fila:Consumidores"] = "1",
                ["Fila:LimiteBanco"] = "1"
            });
        });

        builder.ConfigureServices(services =>
        {
            // remove tudo que o AddDbContext do Postgres registrou
            var antigos = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                    || (d.ServiceType.IsGenericType && d.ServiceType.GenericTypeArguments.Contains(typeof(AppDbContext))))
                .ToList();

            foreach (var descritor in antigos)
            {
                services.Remove(descritor);
            }

            services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={_arquivo}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_arquivo))
            {
                File.Delete(_arquivo);
            }
        }
    }
}
