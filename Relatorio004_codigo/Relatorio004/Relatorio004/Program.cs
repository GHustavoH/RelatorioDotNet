using System.Threading.Channels;
using Relatorio004.Models;
using Relatorio004.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

int capacidade = builder.Configuration.GetValue<int>("Fila:Capacidade", 10);
string modoTexto = builder.Configuration.GetValue<string>("Fila:FullMode", "Wait")!;
var modo = Enum.Parse<BoundedChannelFullMode>(modoTexto);

builder.Services.AddSingleton<Metricas>();

// fila limitada, unica para toda a aplicacao
builder.Services.AddSingleton(sp =>
    FilaFactory.Criar(capacidade, modo, sp.GetRequiredService<Metricas>()));

builder.Services.AddScoped<IPedidoRepositorio, PedidoRepositorio>();
builder.Services.AddHostedService<PedidoWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
