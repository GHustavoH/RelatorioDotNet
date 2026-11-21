using Hub.Orders.Domain;

namespace Hub.Orders.Ports;

// Porta de saída pertencente ao módulo de Pedidos
// A implementação vive em Hub.Infrastructure.
public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    Task<Order?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
}
