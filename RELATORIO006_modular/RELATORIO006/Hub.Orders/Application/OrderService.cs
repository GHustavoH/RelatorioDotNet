using Hub.Orders.Domain;
using Hub.Orders.Ports;

namespace Hub.Orders.Application;

public sealed class PlaceOrderCommand
{
    public required string CustomerName { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
}

public interface IOrderService
{
    Task<Guid> PlaceAsync(PlaceOrderCommand command, CancellationToken cancellationToken = default);
    Task<Order?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}

// Todas as dependências chegam por construtor 
// Lifetime registrado: Scoped 
public sealed class OrderService(
    IOrderRepository repository,
    IOrderEventPublisher events,
    TimeProvider clock) : IOrderService
{
    public async Task<Guid> PlaceAsync(PlaceOrderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = Order.Place(
            command.CustomerName,
            new Money(command.Amount, command.Currency),
            clock.GetUtcNow());

        await repository.AddAsync(order, cancellationToken);
        await events.PublishPlacedAsync(new OrderPlaced(order.Id), cancellationToken);

        return order.Id;
    }

    public Task<Order?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => repository.GetAsync(id, cancellationToken);
}
