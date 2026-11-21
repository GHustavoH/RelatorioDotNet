using System.Collections.Concurrent;
using Hub.Orders.Domain;
using Hub.Orders.Ports;

namespace Hub.Infrastructure.Orders;

/// <summary>Singleton: guarda o estado em memória; não depende de nenhum serviço scoped.</summary>
internal sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();

    public Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (!_orders.TryAdd(order.Id, order))
            throw new InvalidOperationException($"Pedido {order.Id} já existe.");

        return Task.CompletedTask;
    }

    public Task<Order?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_orders.GetValueOrDefault(id));

    public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _orders[order.Id] = order;
        return Task.CompletedTask;
    }
}
