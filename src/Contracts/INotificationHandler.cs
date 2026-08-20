namespace LoomKit.Notifications.Contracts;

public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken = default);
}