namespace Hub.Orders.Domain;

public sealed class Order
{
    public Guid Id { get; }
    public string CustomerName { get; }
    public Money Total { get; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private Order(Guid id, string customerName, Money total, DateTimeOffset createdAt)
    {
        Id = id;
        CustomerName = customerName;
        Total = total;
        CreatedAt = createdAt;
        Status = OrderStatus.Pending;
    }

    public static Order Place(string customerName, Money total, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("O nome do cliente é obrigatório.", nameof(customerName));

        ArgumentNullException.ThrowIfNull(total);

        return new Order(Guid.NewGuid(), customerName.Trim(), total, now);
    }

    public void MarkProcessed()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Pedido {Id} não pode ser processado a partir do estado {Status}.");

        Status = OrderStatus.Processed;
    }
}
