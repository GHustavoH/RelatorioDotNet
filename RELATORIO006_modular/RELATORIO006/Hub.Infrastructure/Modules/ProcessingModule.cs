using Hub.Infrastructure.Processing;
using Hub.Orders.Ports;
using Hub.Processing;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Infrastructure.Modules;

public static class ProcessingModule
{
    /// <summary>
    /// Lifetimes:
    ///  - IProcessingNotifier .. Transient (adaptador sem estado)
    ///  - IOrderPlacedHandler .. Scoped    (uma unidade de trabalho por requisição)
    /// </summary>
    public static IServiceCollection AddProcessingModule(this IServiceCollection services)
    {
        services.AddTransient<IProcessingNotifier, ProcessingNotifierAdapter>();
        services.AddScoped<IOrderPlacedHandler, ProcessOrderHandler>();
        return services;
    }
}
