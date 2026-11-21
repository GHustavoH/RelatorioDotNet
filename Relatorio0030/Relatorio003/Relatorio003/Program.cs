using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Relatorio003.Dados;
using Relatorio003.Servicos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

// A senha NÃO fica no repositório: user-secrets ou variável ConnectionStrings__Pedidos.
var connectionString = builder.Configuration.GetConnectionString("Pedidos")
    ?? throw new InvalidOperationException("ConnectionStrings:Pedidos não configurada.");

// Sem EnableSensitiveDataLogging: o log de comandos mostra o SQL, mas não os valores dos parâmetros.
builder.Services.AddDbContext<PedidosDbContext>(o => o.UseNpgsql(connectionString));

builder.Services.AddScoped<PedidoPainelService>();
builder.Services.AddScoped<PedidoRelatorioService>();
builder.Services.AddScoped<PedidoComandoService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // dotnet run -- --seed=20000  : aplica migrations e gera massa de dados (somente Development).
    var seedArg = args.FirstOrDefault(a => a.StartsWith("--seed=", StringComparison.Ordinal));
    if (seedArg is not null && int.TryParse(seedArg["--seed=".Length..], out var quantidade))
    {
        using var escopo = app.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<PedidosDbContext>();
        await db.Database.MigrateAsync();
        await GeradorDeMassa.GerarAsync(db, quantidade);
    }
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
