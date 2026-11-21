using Hub.Orders.Ports;

namespace Hub.Processing;

//Porta de saída pertencente a Processamento (quem consome define a interface).
// A implementação (adaptador para Notificações) vive em Hub.Infrastructure,
// evitando referência direta Processamento - Notificações.
public interface IProcessingNotifier
{
    Task OrderProcessedAsync(Guid orderId, string customerName, CancellationToken cancellationToken = default);
}

public sealed class ProcessOrderHandler(
    IOrderRepository repository,
    IProcessingNotifier notifier) : IOrderPlacedHandler
{
    public async Task HandleAsync(OrderPlaced orderPlaced, CancellationToken cancellationToken = default)
    {
        var order = await repository.GetAsync(orderPlaced.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Pedido {orderPlaced.OrderId} não encontrado para processamento.");

        order.MarkProcessed();
        await repository.UpdateAsync(order, cancellationToken);

        await notifier.OrderProcessedAsync(order.Id, order.CustomerName, cancellationToken);
    }
}
