namespace Hub.Orders.Ports;

//Evento publicado pelo módulo de Pedidos quando um pedido é registrado
public sealed record OrderPlaced(Guid OrderId);


// o módulo de Pedidos publica eventos sem conhecer quem os trata.
// Implementada em Hub.Infrastructure.

public interface IOrderEventPublisher
{
    Task PublishPlacedAsync(OrderPlaced orderPlaced, CancellationToken cancellationToken = default);
}

//Contrato de assinatura 
// a dependência aponta do assinante PARA Pedidos
public interface IOrderPlacedHandler
{
    Task HandleAsync(OrderPlaced orderPlaced, CancellationToken cancellationToken = default);
}
