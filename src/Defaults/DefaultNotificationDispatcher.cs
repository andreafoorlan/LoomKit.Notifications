using System.Collections.Concurrent;
using System.Diagnostics;
using LoomKit.Notifications.Abstracts;
using LoomKit.Notifications.Contracts;
using LoomKit.Notifications.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace LoomKit.Notifications.Defaults;

public class DefaultNotificationDispatcher : NotificationDispatcher<DefaultNotificationDispatcherOptions>
{
    // Pipeline "plans" (which handler/middleware types to close and how to build them) only
    // depend on the notification types and on the options instance, never on a specific
    // notification value, so they can be computed once and reused for every DispatchAsync call instead
    // of re-running reflection (GetMethod/MakeGenericType/MakeGenericMethod) each time.
    // Keyed on the options instance (reference equality) rather than globally, so two
    // differently configured dispatchers of this same concrete type don't share a cache entry.
    private static readonly ConcurrentDictionary<(NotificationDispatcherOptions Options, Type NotificationType), NotificationPlan> _notificationPlanCache = new();

    private readonly IServiceProvider _serviceProvider;

    public DefaultNotificationDispatcher(DefaultNotificationDispatcherOptions notificationDispatcherOptions, IServiceProvider serviceProvider)
        : base(notificationDispatcherOptions)
    {
        // deps
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    public override async Task DispatchAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
    {
        await InnerNotificationDispatchAsync(notification, cancellationToken);
    }

    protected virtual async Task InnerNotificationDispatchAsync<TNotification>(TNotification notification, CancellationToken? cancellationToken = null)
    where TNotification : INotification
    {
        // get notification type
        var notificationType = typeof(TNotification);

        // get (or build once) the plan describing which handler/middleware types to close and how to construct them
        var plan = _notificationPlanCache.GetOrAdd((_notificationDispatcherOptions, notificationType), static key =>
        {
            // build concrete handler type
            var (options, notificationType) = key;
            var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);

            // built in reverse so the first-registered middleware ends up outermost (executes first)
            var middlewareFactories = options.NotificationMiddlewareTypes
                .Reverse()
                .Select(middlewareType => ActivatorUtilities.CreateFactory(middlewareType.MakeGenericType(notificationType), [handlerType]))
                .ToArray();

            return new NotificationPlan(handlerType, middlewareFactories);

        });

        // get notification handler instances
        var notificationHandlerClosedInstances = _serviceProvider.GetServices(plan.HandlerType)
            .Select(handler => (INotificationHandler<TNotification>)handler!);

        // 
        using var dispatchActivity = NotificationsActivitySource.Source.StartActivity($"notification.dispatch {notificationType.Name}", ActivityKind.Internal);
        dispatchActivity?.SetTag("notification.type", notificationType.FullName);

        // create the middleware pipeline from middleware types for each notification handler resolved with DI
        foreach (var notificationHandlerClosedInstance in notificationHandlerClosedInstances)
        {
            var currentHandler = notificationHandlerClosedInstance;
            var handlerType = notificationHandlerClosedInstance!.GetType();

            foreach (var middlewareFactory in plan.MiddlewareFactories)
            {
                currentHandler = (INotificationHandler<TNotification>)middlewareFactory(_serviceProvider, [currentHandler]);
            }

            using var handleActivity = NotificationsActivitySource.Source.StartActivity($"notification.handle {notificationType.Name}", ActivityKind.Internal);
            handleActivity?.SetTag("notification.type", notificationType.FullName);
            handleActivity?.SetTag("handler.type", handlerType.FullName);

            try
            {
                await currentHandler.HandleAsync(notification, cancellationToken ?? CancellationToken.None);
            }
            catch (Exception exception)
            {
                RecordException(handleActivity, exception);
                throw;
            }
        }
    }


    // Records the exception on the Activity using the framework's own OpenTelemetry-conformant
    // helper instead of hand-building tags. Note for consumers: this attaches the full exception
    // (including its Message and stack trace) to the Activity, so it will flow to whatever
    // tracing backend/exporter is configured - avoid throwing exceptions that carry secrets or
    // PII in their Message from notification handlers if that telemetry pipeline isn't access-controlled
    // the same way your application logs are.
    private static void RecordException(Activity? activity, Exception exception)
    {
        if (activity is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddException(exception);
    }

    private sealed record NotificationPlan(Type HandlerType, ObjectFactory[] MiddlewareFactories);
}