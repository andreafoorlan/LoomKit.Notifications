using LoomKit.Notifications.Abstracts;
using LoomKit.Notifications.Contracts;

namespace LoomKit.Notifications.Tests.Fixtures;

public sealed class PingNotification : INotification
{
    public List<string> Trace { get; } = [];

    public CancellationToken ObservedToken { get; set; }
}

// Two handlers for the same notification type, to exercise the pub/sub fan-out
// (unlike Requests, a notification can have more than one handler resolved via DI).
public sealed class FirstPingNotificationHandler : INotificationHandler<PingNotification>
{
    public Task HandleAsync(PingNotification notification, CancellationToken cancellationToken = default)
    {
        notification.Trace.Add("first-handler");
        notification.ObservedToken = cancellationToken;

        return Task.CompletedTask;
    }
}

public sealed class SecondPingNotificationHandler : INotificationHandler<PingNotification>
{
    public Task HandleAsync(PingNotification notification, CancellationToken cancellationToken = default)
    {
        notification.Trace.Add("second-handler");

        return Task.CompletedTask;
    }
}

public sealed class FirstMiddleware<TNotification> : NotificationMiddleware<TNotification>
    where TNotification : INotification
{
    public FirstMiddleware(INotificationHandler<TNotification> nextHandler) : base(nextHandler) { }

    public override async Task HandleAsync(TNotification notification, CancellationToken cancellationToken = default)
    {
        if (notification is PingNotification ping) ping.Trace.Add("first:before");

        await _nextHandler.HandleAsync(notification, cancellationToken);

        if (notification is PingNotification pingAfter) pingAfter.Trace.Add("first:after");
    }
}

public sealed class SecondMiddleware<TNotification> : NotificationMiddleware<TNotification>
    where TNotification : INotification
{
    public SecondMiddleware(INotificationHandler<TNotification> nextHandler) : base(nextHandler) { }

    public override async Task HandleAsync(TNotification notification, CancellationToken cancellationToken = default)
    {
        if (notification is PingNotification ping) ping.Trace.Add("second:before");

        await _nextHandler.HandleAsync(notification, cancellationToken);

        if (notification is PingNotification pingAfter) pingAfter.Trace.Add("second:after");
    }
}
