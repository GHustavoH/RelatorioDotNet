using Hub.Infrastructure.Notifications;
using Hub.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Infrastructure.Modules;

public static class NotificationsModule
{
    /// <summary>
    /// Lifetimes:
    ///  - INotificationSender .. Singleton (sem estado)
    ///  - INotificationService . Singleton (sem estado; só depende de singleton)
    /// </summary>
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddSingleton<INotificationSender, LogNotificationSender>();
        services.AddSingleton<INotificationService, NotificationService>();
        return services;
    }
}
