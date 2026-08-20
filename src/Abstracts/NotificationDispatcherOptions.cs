using Microsoft.Extensions.DependencyInjection;

namespace LoomKit.Notifications.Abstracts;

public abstract class NotificationDispatcherOptions
{
    /*
    * The lifetime of the NotificationDispatcher service in the DI
    */
    public ServiceLifetime ServiceLifetime { get; init; } = ServiceLifetime.Scoped;

    /*
    * Read-Only list of middlewares executed on each notification, can be modified earlier using builder
    */
    public IReadOnlyList<Type> NotificationMiddlewareTypes { get; init; } = [];
}