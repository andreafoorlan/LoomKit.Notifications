using System.Collections.Immutable;

namespace LoomKit.Notifications.Abstracts;

public abstract class NotificationDispatcherOptionsBuilder<TNotificationDispatcherOptions>
    where TNotificationDispatcherOptions : NotificationDispatcherOptions, new()
{
    private LinkedList<Type> _notificationMiddlewareTypes;

    public NotificationDispatcherOptionsBuilder()
    {
        // inits
        _notificationMiddlewareTypes = new LinkedList<Type>();
    }

    public NotificationDispatcherOptionsBuilder<TNotificationDispatcherOptions> ClearNotificationMiddlewares()
    {
        // clear
        _notificationMiddlewareTypes.Clear();

        // 
        return this;
    }

    public NotificationDispatcherOptionsBuilder<TNotificationDispatcherOptions> UseNotificationMiddleware(Type notificationMiddlewareType)
    {
        // check type is null
        if (notificationMiddlewareType is null)
            throw new ArgumentNullException(nameof(notificationMiddlewareType));

        // check if middlewareType is open generic type
        if (!notificationMiddlewareType.IsGenericTypeDefinition)
        {
            throw new ArgumentException("Middleware type must be an open generic type", nameof(notificationMiddlewareType));
        }

        // check if middlewareType implements JobMiddleware<>
        if (!DerivesFromOpenGeneric(notificationMiddlewareType, typeof(NotificationMiddleware<>)))
            throw new ArgumentException("Middleware type must implement NotificationMiddleware<>", nameof(notificationMiddlewareType));

        // add as last to list
        _notificationMiddlewareTypes.AddLast(notificationMiddlewareType);

        //
        return this;
    }

    public virtual NotificationDispatcherOptions Build()
    {
        //
        return new TNotificationDispatcherOptions()
        {
            NotificationMiddlewareTypes = _notificationMiddlewareTypes.ToImmutableList(),
        };
    }

    // Walks the base-type chain (not GetInterfaces(), since NotificationMiddleware<> 
    // is an abstract class, not an interface) looking for a base type
    // closing the given open generic definition.
    private static bool DerivesFromOpenGeneric(Type type, Type openGenericBaseType)
    {
        for (var currentBaseType = type.BaseType; currentBaseType is not null; currentBaseType = currentBaseType.BaseType)
        {
            if (currentBaseType.IsGenericType && currentBaseType.GetGenericTypeDefinition() == openGenericBaseType)
                return true;
        }

        return false;
    }
}