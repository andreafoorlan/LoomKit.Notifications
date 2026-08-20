using LoomKit.Notifications.Contracts;

namespace LoomKit.Notifications.Abstracts;

public abstract class NotificationMiddleware<TNotification> : INotificationHandler<TNotification>
    where TNotification : INotification
{
    protected readonly INotificationHandler<TNotification> _nextHandler;

    protected NotificationMiddleware(INotificationHandler<TNotification> nextHandler)
    {
        // deps
        ArgumentNullException.ThrowIfNull(nextHandler);
        _nextHandler = nextHandler;
    }

    public abstract Task HandleAsync(TNotification notification, CancellationToken cancellationToken = default);
}