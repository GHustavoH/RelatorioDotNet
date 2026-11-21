using Hub.Orders.Ports;

namespace Hub.Infrastructure.Orders;

/// <summary>
/// Scoped: recebe por construtor todos os assinantes (IEnumerable), sem resolver
/// serviços manualmente. Os assinantes são Scoped, portanto este publicador também é.
/// </summary>
internal sealed class InMemoryOrderEventPublisher(IEnumerable<IOrderPlacedHandler> handlers) : IOrderEventPublisher
{
    public async Task PublishPlacedAsync(OrderPlaced orderPlaced, CancellationToken cancellationToken = default)
    {
        foreach (var handler in handlers)
            await handler.HandleAsync(orderPlaced, cancellationToken);
    }
}
