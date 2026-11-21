using RelatorioFinal.Modulos;
using RelatorioFinal.Persistencia;
using RelatorioFinal.TempoReal;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// composition root com um registro por modulo
builder.Services.AddModuloSeguranca();
builder.Services.AddModuloPedidos();
builder.Services.AddModuloProcessamento();
builder.Services.AddModuloNotificacoes();

// cache de saida com politica explicita e tag para invalidar quando um pedido muda
builder.Services.AddOutputCache(opcoes =>
{
    opcoes.AddPolicy("Estatisticas", politica => politica.Expire(TimeSpan.FromSeconds(10)).Tag("pedidos"));
});

var app = builder.Build();

// cria o esquema se ainda nao existir, para usar migrations troque por Migrate
using (var escopo = app.Services.CreateScope())
{
    escopo.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

// o cache de saida vem depois da autenticacao e da autorizacao
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

app.MapControllers();
app.MapHub<PedidosHub>("/hubs/pedidos");

app.Run();

// necessario para o WebApplicationFactory dos testes
public partial class Program { }
