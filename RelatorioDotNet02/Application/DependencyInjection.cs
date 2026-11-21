using HubPedidos.Application.Pedidos;
using HubPedidos.Application.Processamento;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HubPedidos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddHubPedidosApplication(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        var assemblyConhecido = typeof(IProcessadorPedido).Assembly;
        var catalogo = CatalogoProcessadores.Construir(assemblyConhecido, exigirCobertura: true);

        services.AddSingleton(catalogo);
        foreach (var descritor in catalogo.Descritores)
        {
            services.AddSingleton(descritor.Tipo);
        }

        services.AddScoped<ServicoPedidos>();
        return services;
    }
}
