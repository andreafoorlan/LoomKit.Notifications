using System.Diagnostics;
using System.Reflection;

namespace LoomKit.Notifications.Telemetry;

internal static class NotificationsActivitySource
{
    private static readonly AssemblyName AssemblyName = typeof(NotificationsActivitySource).Assembly.GetName();

    internal static readonly ActivitySource Source = new(
        AssemblyName.Name ?? "LoomKit.Notifications",
        AssemblyName.Version?.ToString() ?? "1.0.0");
}
