namespace LoomKit.Notifications.Contracts;

public interface INotificationDispatcher
{
    Task DispatchAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}