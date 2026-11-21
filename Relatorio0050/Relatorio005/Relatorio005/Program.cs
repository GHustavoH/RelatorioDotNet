using Microsoft.Extensions.Options;
using Relatorio005.Hub;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<HubOptions>()
    .Bind(builder.Configuration.GetSection(HubOptions.Secao))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<HubMetricas>();
builder.Services.AddSingleton(provedor =>
{
    var opcoes = provedor.GetRequiredService<IOptions<HubOptions>>().Value;
    var store = new MensagemStore(opcoes.CapacidadeArmazenamento);
    var quantidade = Math.Min(opcoes.SeedQuantidade, opcoes.CapacidadeArmazenamento);
    store.Carregar(GeradorMassaDados.Gerar(quantidade, opcoes.SeedAleatoria));
    return store;
});
builder.Services.AddSingleton<FilaMensagens>();
builder.Services.AddHostedService<ProcessadorMensagensService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();

public partial class Program;
