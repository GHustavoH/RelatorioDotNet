namespace Hub.Notifications;

public sealed record Notification(string Recipient, string Subject, string Body);

//Porta de saída do módulo de Notificações (implementada em Hub.Infrastructure)
public interface INotificationSender
{
    Task SendAsync(Notification notification, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task NotifyOrderProcessedAsync(Guid orderId, string customerName, CancellationToken cancellationToken = default);
}

// Sem estado e dependente apenas de singleton: registrado como Singleton.
public sealed class NotificationService(INotificationSender sender) : INotificationService
{
    public Task NotifyOrderProcessedAsync(Guid orderId, string customerName, CancellationToken cancellationToken = default)
    {
        var notification = new Notification(
            Recipient: customerName,
            Subject: $"Pedido {orderId} processado",
            Body: "Seu pedido foi processado com sucesso.");

        return sender.SendAsync(notification, cancellationToken);
    }
}
