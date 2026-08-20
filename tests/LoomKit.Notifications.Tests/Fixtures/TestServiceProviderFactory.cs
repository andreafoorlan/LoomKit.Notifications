using LoomKit.Notifications.Contracts;
using LoomKit.Notifications.Defaults;
using Microsoft.Extensions.DependencyInjection;

namespace LoomKit.Notifications.Tests.Fixtures;

internal static class TestServiceProviderFactory
{
    public static ServiceProvider Build(Action<DefaultNotificationDispatcherOptionsBuilder>? configure = null)
    {
        var services = new ServiceCollection();

        services.AddScoped<INotificationHandler<PingNotification>, FirstPingNotificationHandler>();
        services.AddScoped<INotificationHandler<PingNotification>, SecondPingNotificationHandler>();

        services.AddDefaultNotificationDispatcher(configure ?? (_ => { }));

        return services.BuildServiceProvider();
    }
}
