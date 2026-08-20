using LoomKit.Notifications.Defaults;
using LoomKit.Notifications.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LoomKit.Notifications.Tests;

public class NotificationDispatcherOptionsBuilderTests
{
    [Fact]
    public void UseNotificationMiddleware_ThrowsArgumentNullException_WhenTypeIsNull()
    {
        var builder = new DefaultNotificationDispatcherOptionsBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.UseNotificationMiddleware(null!));
    }

    [Fact]
    public void UseNotificationMiddleware_ThrowsArgumentException_WhenTypeIsNotOpenGeneric()
    {
        var builder = new DefaultNotificationDispatcherOptionsBuilder();

        Assert.Throws<ArgumentException>(() => builder.UseNotificationMiddleware(typeof(FirstMiddleware<PingNotification>)));
    }

    [Fact]
    public void UseNotificationMiddleware_ThrowsArgumentException_WhenTypeDoesNotImplementNotificationMiddleware()
    {
        var builder = new DefaultNotificationDispatcherOptionsBuilder();

        Assert.Throws<ArgumentException>(() => builder.UseNotificationMiddleware(typeof(List<>)));
    }

    [Fact]
    public void ClearNotificationMiddlewares_RemovesPreviouslyAddedMiddlewares()
    {
        var builder = new DefaultNotificationDispatcherOptionsBuilder()
            .UseNotificationMiddleware(typeof(FirstMiddleware<>))
            .ClearNotificationMiddlewares();

        var options = builder.Build();

        Assert.Empty(options.NotificationMiddlewareTypes);
    }

    [Fact]
    public void Build_DefaultsToScopedServiceLifetime()
    {
        var builder = new DefaultNotificationDispatcherOptionsBuilder();

        var options = builder.Build();

        Assert.Equal(ServiceLifetime.Scoped, options.ServiceLifetime);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void WithLifetime_OverridesServiceLifetime_OnBuiltOptions(ServiceLifetime lifetime)
    {
        var builder = new DefaultNotificationDispatcherOptionsBuilder()
            .WithLifetime(lifetime);

        var options = builder.Build();

        Assert.Equal(lifetime, options.ServiceLifetime);
    }
}
