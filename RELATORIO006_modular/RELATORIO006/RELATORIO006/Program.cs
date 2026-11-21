using Hub.Infrastructure.Modules;
using RELATORIO006.Versioning;

var builder = WebApplication.CreateBuilder(args);

// ===== COMPOSITION ROOT =====
// Único lugar que conhece todos os módulos. Cada módulo expõe uma extensão de registro.
builder.Services.AddControllers();
builder.Services.AddHubApiVersioning(builder.Configuration);

builder.Services.AddOrdersModule();
builder.Services.AddNotificationsModule();
builder.Services.AddProcessingModule();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // /openapi/v1.json e /openapi/v2.json
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// A versão é resolvida ANTES do roteamento para a seleção de ação por versão.
app.UseApiVersioning();
app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Necessário para WebApplicationFactory<Program> nos testes.
public partial class Program { }
