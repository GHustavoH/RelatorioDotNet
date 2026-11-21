using Microsoft.AspNetCore.Authentication;
using relatorio007.Auth;
using relatorio007.Hubs;
using relatorio007.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<PedidoStore>();
builder.Services.AddSingleton<Assinaturas>();

builder.Services.AddAuthentication(HeaderAuthHandler.Esquema)
    .AddScheme<AuthenticationSchemeOptions, HeaderAuthHandler>(HeaderAuthHandler.Esquema, null);
builder.Services.AddAuthorization();

builder.Services.AddSignalR();

// cache so da leitura publica: 30s, varia por id da rota e por query "formato", tag "pedidos" para invalidar
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("PedidoPublico", policy => policy
        .Expire(TimeSpan.FromSeconds(30))
        .SetVaryByRouteValue("id")
        .SetVaryByQuery("formato")
        .Tag("pedidos"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

app.MapControllers();
app.MapHub<PedidosHub>("/hubs/pedidos");

app.Run();

public partial class Program { }
