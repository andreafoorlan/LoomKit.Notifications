using System.Reflection;
using LoomKit.Notifications.Abstracts;
using LoomKit.Notifications.Contracts;
using LoomKit.Notifications.Defaults;
using Microsoft.Extensions.DependencyInjection;

namespace LoomKit.Notifications;

public static class DependencyInjection
{
    private static readonly Type[] _notificationHandlerOpenGenericTypes = [typeof(INotificationHandler<>)];
    
    public static IServiceCollection AddNotificationDispatcher<TNotificationDispatcher, TNotificationDispatcherOptionsBuilder, TNotificationDispatcherOptions>(this IServiceCollection services, Action<TNotificationDispatcherOptionsBuilder> optionsBuilder)
        where TNotificationDispatcher : NotificationDispatcher<TNotificationDispatcherOptions>
        where TNotificationDispatcherOptionsBuilder : NotificationDispatcherOptionsBuilder<TNotificationDispatcherOptions>, new()
        where TNotificationDispatcherOptions : NotificationDispatcherOptions, new()
    {
        // create option builder
        var notificationDispatcherOptionsBuilder = new TNotificationDispatcherOptionsBuilder();

        // invoke build action
        optionsBuilder.Invoke(notificationDispatcherOptionsBuilder);

        // build notification options from builder
        var notificationDispatcherOptions = notificationDispatcherOptionsBuilder.Build();

        // define instace builder
        Func<IServiceProvider, INotificationDispatcher> instanceBuilder = (serviceProvider) =>
        {
            return (INotificationDispatcher)ActivatorUtilities.CreateInstance(serviceProvider, typeof(TNotificationDispatcher), notificationDispatcherOptions);
        };

        // add notification sender to DI
        _ = notificationDispatcherOptions.ServiceLifetime switch
        {
            ServiceLifetime.Singleton => services.AddSingleton<INotificationDispatcher>(serviceProvider => instanceBuilder(serviceProvider)),
            ServiceLifetime.Scoped => services.AddScoped<INotificationDispatcher>(serviceProvider => instanceBuilder(serviceProvider)),
            ServiceLifetime.Transient => services.AddTransient<INotificationDispatcher>(serviceProvider => instanceBuilder(serviceProvider)),
            _ => throw new NotImplementedException()
        };

        //
        return services;
    }

    public static IServiceCollection AddDefaultNotificationDispatcher(this IServiceCollection services, Action<DefaultNotificationDispatcherOptionsBuilder> optionsBuilder)
    {
        // call generic add method
        return AddNotificationDispatcher<DefaultNotificationDispatcher, DefaultNotificationDispatcherOptionsBuilder, DefaultNotificationDispatcherOptions>(services, optionsBuilder);
    }

    // Scans the given assemblies for concrete, closed INotificationHandler<> implementations
    // and registers each one against the interface(s) it implements. Open-generic handlers (rare) are not
    // discovered - register those manually with services.AddScoped(typeof(INotificationHandler<>), typeof(MyHandler<>)).
    public static IServiceCollection AddNotificationHandlersFromAssemblies(this IServiceCollection services, ServiceLifetime lifetime, params Assembly[] assemblies)
    {
        // check args
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
            ArgumentNullException.ThrowIfNull(assembly, nameof(assemblies));

        // track (interface, implementation) pairs already registered so overlapping assemblies,
        // or calling this method more than once against the same IServiceCollection, don't produce
        // duplicate registrations - seeded from what's already in `services` (including registrations
        // from an earlier call to this method, or a handler registered manually beforehand)
        var registered = new HashSet<(Type HandlerInterface, Type ImplementationType)>(
            services
                .Where(d => d.ImplementationType is not null)
                .Select(d => (d.ServiceType, d.ImplementationType!)));

        foreach (var assembly in assemblies)
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                // only concrete, closed classes can be instantiated as-is; open-generic handlers are out of scope here
                if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                    continue;

                foreach (var handlerInterface in GetClosedNotificationHandlerInterfaces(type))
                {
                    if (registered.Add((handlerInterface, type)))
                    {
                        services.Add(ServiceDescriptor.Describe(handlerInterface, type, lifetime));
                    }
                }
            }
        }

        //
        return services;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // some types in the assembly could not be loaded (e.g. missing dependency) - use the ones that could
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static IEnumerable<Type> GetClosedNotificationHandlerInterfaces(Type type)
    {
        return type
            .GetInterfaces()
            .Where(i => i.IsGenericType && _notificationHandlerOpenGenericTypes.Contains(i.GetGenericTypeDefinition()));
    }
}