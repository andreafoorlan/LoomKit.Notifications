using LoomKit.Notifications.Contracts;

namespace LoomKit.Notifications.Abstracts;

public abstract class NotificationDispatcher<TNotificationDispatcherOptions> : INotificationDispatcher
    where TNotificationDispatcherOptions : NotificationDispatcherOptions
{
    protected readonly TNotificationDispatcherOptions _notificationDispatcherOptions;

    protected NotificationDispatcher(TNotificationDispatcherOptions notificationDispatcherOptions)
    {
        // deps
        ArgumentNullException.ThrowIfNull(notificationDispatcherOptions);
        _notificationDispatcherOptions = notificationDispatcherOptions;
    }

    public abstract Task DispatchAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}