using Hub.Notifications;
using Microsoft.Extensions.Logging;

namespace Hub.Infrastructure.Notifications;

/// <summary>Singleton: sem estado. Não registra destinatário nem corpo (dados pessoais).</summary>
internal sealed class LogNotificationSender(ILogger<LogNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Notificação enviada: {Subject}", notification.Subject);
        return Task.CompletedTask;
    }
}
