using System.Diagnostics;
using System.Reflection;

namespace LoomKit.Notifications.Telemetry;

internal static class NotificationsActivatorSource
{
    private static readonly AssemblyName AssemblyName = typeof(NotificationsActivatorSource).Assembly.GetName();

    internal static readonly ActivitySource Source = new(
        AssemblyName.Name ?? "LoomKit.Notificatioons",
        AssemblyName.Version?.ToString() ?? "1.0.0");
}