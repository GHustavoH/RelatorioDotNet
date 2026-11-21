using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RelatorioFinal.Contratos;
using RelatorioFinal.Dominio;
using RelatorioFinal.Fila;
using RelatorioFinal.Persistencia;
using RelatorioFinal.Seguranca;
using RelatorioFinal.Servicos;
using RelatorioFinal.TempoReal;

namespace RelatorioFinal.Modulos;

// cada modulo expoe um metodo de extensao chamado pelo composition root no Program
public static class ExtensoesModulos
{
    public static IServiceCollection AddModuloSeguranca(this IServiceCollection services)
    {
        services.AddSingleton<JwtServico>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorization();

        // a configuracao e lida so quando o servico e usado o que facilita trocar valores nos testes
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration>((opcoes, config) =>
            {
                opcoes.MapInboundClaims = false;
                opcoes.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = JwtServico.Emissor,
                    ValidateAudience = true,
                    ValidAudience = JwtServico.Emissor,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Chave"]!)),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };

                // o SignalR envia o token na query string
                opcoes.Events = new JwtBearerEvents
                {
                    OnMessageReceived = contexto =>
                    {
                        var token = contexto.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && contexto.HttpContext.Request.Path.StartsWithSegments("/hubs/pedidos"))
                        {
                            contexto.Token = token;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    public static IServiceCollection AddModuloPedidos(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>((sp, opcoes) =>
            opcoes.UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Padrao")));

        services.AddScoped<IPedidoServico, PedidoServico>();
        return services;
    }

    public static IServiceCollection AddModuloProcessamento(this IServiceCollection services)
    {
        // o catalogo e criado aqui e falha na inicializacao se houver duplicidade ou atributo faltando
        services.AddSingleton(new CatalogoProcessadores(typeof(CatalogoProcessadores).Assembly));

        // fila limitada unica com Wait como modo de fila cheia, quem usa TryWrite recebe false
        services.AddSingleton(sp =>
        {
            var capacidade = sp.GetRequiredService<IConfiguration>().GetValue("Fila:Capacidade", 100);
            var opcoes = new BoundedChannelOptions(capacidade) { FullMode = BoundedChannelFullMode.Wait };
            return Channel.CreateBounded<ProcessarPedidoJob>(opcoes);
        });

        services.AddSingleton<MetricasFila>();
        services.AddHostedService<ProcessadorPedidosWorker>();
        return services;
    }

    public static IServiceCollection AddModuloNotificacoes(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddScoped<INotificadorPedidos, NotificadorSignalR>();
        return services;
    }
}
