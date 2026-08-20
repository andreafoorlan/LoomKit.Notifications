using LoomKit.Notifications.Contracts;
using LoomKit.Notifications.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LoomKit.Notifications.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public async Task AddNotificationHandlersFromAssemblies_RegistersHandlers_ResolvableEndToEnd()
    {
        var services = new ServiceCollection();
        services.AddNotificationHandlersFromAssemblies(ServiceLifetime.Scoped, typeof(FirstPingNotificationHandler).Assembly);
        services.AddDefaultNotificationDispatcher(_ => { });

        using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<INotificationDispatcher>();

        var notification = new PingNotification();
        await dispatcher.DispatchAsync(notification);

        Assert.Equal(["first-handler", "second-handler"], notification.Trace);
    }

    [Fact]
    public void AddNotificationHandlersFromAssemblies_DoesNotRegister_OpenGenericMiddlewareClasses()
    {
        var services = new ServiceCollection();
        services.AddNotificationHandlersFromAssemblies(ServiceLifetime.Scoped, typeof(FirstPingNotificationHandler).Assembly);

        // FirstMiddleware<>/SecondMiddleware<> also implement INotificationHandler<TNotification>, but as open
        // generics they must never be picked up by the scan and registered as if they were handlers.
        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(FirstMiddleware<>));
        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(SecondMiddleware<>));
    }

    [Fact]
    public void AddNotificationHandlersFromAssemblies_DoesNotDuplicate_WhenCalledTwiceOrGivenOverlappingAssemblies()
    {
        var services = new ServiceCollection();
        var assembly = typeof(FirstPingNotificationHandler).Assembly;

        services.AddNotificationHandlersFromAssemblies(ServiceLifetime.Scoped, assembly, assembly);
        services.AddNotificationHandlersFromAssemblies(ServiceLifetime.Scoped, assembly);

        var registrations = services.Count(d => d.ServiceType == typeof(INotificationHandler<PingNotification>) && d.ImplementationType == typeof(FirstPingNotificationHandler));

        Assert.Equal(1, registrations);
    }

    [Fact]
    public void AddNotificationHandlersFromAssemblies_RespectsRequestedServiceLifetime()
    {
        var services = new ServiceCollection();
        services.AddNotificationHandlersFromAssemblies(ServiceLifetime.Singleton, typeof(FirstPingNotificationHandler).Assembly);

        var descriptor = services.First(d => d.ServiceType == typeof(INotificationHandler<PingNotification>));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddNotificationHandlersFromAssemblies_ThrowsArgumentNullException_WhenAssembliesIsNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddNotificationHandlersFromAssemblies(ServiceLifetime.Scoped, (System.Reflection.Assembly[])null!));
    }
}
