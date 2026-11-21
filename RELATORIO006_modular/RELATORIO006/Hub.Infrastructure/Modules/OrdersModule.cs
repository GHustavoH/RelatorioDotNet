using Hub.Infrastructure.Orders;
using Hub.Orders.Application;
using Hub.Orders.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hub.Infrastructure.Modules;

public static class OrdersModule
{
    /// <summary>
    /// Lifetimes:
    ///  - TimeProvider ........ Singleton (sem estado)
    ///  - IOrderRepository .... Singleton (guarda o estado em memória; não depende de scoped)
    ///  - IOrderEventPublisher  Scoped    (depende de assinantes Scoped)
    ///  - IOrderService ....... Scoped    (depende do publicador Scoped)
    /// </summary>
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        services.AddScoped<IOrderEventPublisher, InMemoryOrderEventPublisher>();
        services.AddScoped<IOrderService, OrderService>();
        return services;
    }
}
