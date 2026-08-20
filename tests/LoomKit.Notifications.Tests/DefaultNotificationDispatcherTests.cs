using LoomKit.Notifications.Contracts;
using LoomKit.Notifications.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LoomKit.Notifications.Tests;

public class DefaultNotificationDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_PropagatesCancellationToken_ToHandlers()
    {
        using var provider = TestServiceProviderFactory.Build();
        var dispatcher = provider.GetRequiredService<INotificationDispatcher>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var notification = new PingNotification();
        await dispatcher.DispatchAsync(notification, cts.Token);

        Assert.True(notification.ObservedToken.IsCancellationRequested);
    }

    [Fact]
    public async Task DispatchAsync_InvokesEveryRegisteredHandler_ForTheNotificationType()
    {
        using var provider = TestServiceProviderFactory.Build();
        var dispatcher = provider.GetRequiredService<INotificationDispatcher>();

        var notification = new PingNotification();
        await dispatcher.DispatchAsync(notification);

        Assert.Equal(["first-handler", "second-handler"], notification.Trace);
    }

    [Fact]
    public async Task DispatchAsync_ExecutesNotificationMiddleware_InRegistrationOrder_ForEachHandler()
    {
        using var provider = TestServiceProviderFactory.Build(builder => builder
            .UseNotificationMiddleware(typeof(FirstMiddleware<>))
            .UseNotificationMiddleware(typeof(SecondMiddleware<>)));
        var dispatcher = provider.GetRequiredService<INotificationDispatcher>();

        var notification = new PingNotification();
        await dispatcher.DispatchAsync(notification);

        Assert.Equal(
            [
                "first:before", "second:before", "first-handler", "second:after", "first:after",
                "first:before", "second:before", "second-handler", "second:after", "first:after",
            ],
            notification.Trace);
    }

    [Fact]
    public async Task DispatchAsync_ReusesCachedPipelinePlan_AcrossMultipleIndependentCalls()
    {
        using var provider = TestServiceProviderFactory.Build(builder => builder
            .UseNotificationMiddleware(typeof(FirstMiddleware<>)));
        var dispatcher = provider.GetRequiredService<INotificationDispatcher>();

        var first = new PingNotification();
        var second = new PingNotification();

        await dispatcher.DispatchAsync(first);
        await dispatcher.DispatchAsync(second);

        var expectedTrace = new[] { "first:before", "first-handler", "first:after", "first:before", "second-handler", "first:after" };
        Assert.Equal(expectedTrace, first.Trace);
        Assert.Equal(expectedTrace, second.Trace);
    }
}
