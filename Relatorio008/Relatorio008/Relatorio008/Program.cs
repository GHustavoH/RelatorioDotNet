using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddAuthentication("ApiKey")
    .AddScheme<AuthenticationSchemeOptions, ApiKeyHandler>("ApiKey", null);
builder.Services.AddAuthorization();

// No ambiente Testing nada disso e registrado: a fabrica de testes
// precisa fornecer banco e fila, assim o banco de dev nunca e usado sem querer.
var ehTeste = builder.Environment.IsEnvironment("Testing");
if (!ehTeste)
{
    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseSqlite(builder.Configuration.GetConnectionString("Default")));
    builder.Services.AddSingleton<FilaEmMemoria>();
    builder.Services.AddSingleton<IFilaPedidos>(sp => sp.GetRequiredService<FilaEmMemoria>());
    builder.Services.AddHostedService<ConsumidorPedidos>();
}

var app = builder.Build();

if (!ehTeste)
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// necessario para o WebApplicationFactory<Program> enxergar a classe
public partial class Program
{
}
