using Hub.Notifications;
using Hub.Processing;

namespace Hub.Infrastructure.Processing;

/// <summary>
/// Adaptador: implementa a porta de Processamento delegando para Notificações.
/// Transient: sem estado, apenas repassa a chamada.
/// </summary>
internal sealed class ProcessingNotifierAdapter(INotificationService notifications) : IProcessingNotifier
{
    public Task OrderProcessedAsync(Guid orderId, string customerName, CancellationToken cancellationToken = default)
        => notifications.NotifyOrderProcessedAsync(orderId, customerName, cancellationToken);
}
