# LoomKit.Notifications

A lightweight in-process notification / pub-sub library for .NET: dispatch a notification to zero, one, or many handlers resolved through dependency injection, with an optional per-notification-type middleware pipeline. If you've used MediatR's `INotification`/`Publish`, the shape will feel familiar — this is the fire-and-forget, fan-out sibling of [LoomKit.Requests](https://github.com/andreafoorlan/LoomKit.Requests).

> **Status:** early stage. The public API may still change between versions — pin a commit/tag if you depend on it.

## Features

- Fan-out notifications (`INotification`) to every handler registered for that type — zero, one, or many
- Handlers resolved from your DI container (`Microsoft.Extensions.DependencyInjection`)
- An optional middleware pipeline per notification type, configured at startup, applied independently to each handler
- Built-in tracing via `System.Diagnostics.ActivitySource` (OpenTelemetry-compatible)
- `CancellationToken` propagated end-to-end, from `DispatchAsync` through every middleware down to each handler
- Extensible: bring your own `INotificationDispatcher` implementation if the default one doesn't fit

## Requirements

- .NET 10 or later
- Depends on [`LoomKit.Notifications.Abstractions`](https://github.com/andreafoorlan/LoomKit.Notifications.Abstractions) (the interfaces and abstract base types, in their own package) and `Microsoft.Extensions.DependencyInjection.Abstractions` — no other runtime dependency

## Architecture: split from `LoomKit.Notifications.Abstractions`

The interfaces (`INotification`, `INotificationHandler<>`, `INotificationDispatcher`) and abstract base types (`NotificationMiddleware<>`, `NotificationDispatcher<>`, `NotificationDispatcherOptions`, `NotificationDispatcherOptionsBuilder<>`) live in the separate, lighter [`LoomKit.Notifications.Abstractions`](https://github.com/andreafoorlan/LoomKit.Notifications.Abstractions) package, which this package references. `LoomKit.Notifications` adds the concrete pieces on top: `DefaultNotificationDispatcher`, the DI registration helpers, assembly-scanning handler discovery, and tracing.

This means a project that only needs to *define* notifications/handlers — typically a domain/DDD class library that shouldn't know how notifications get dispatched — can depend on `LoomKit.Notifications.Abstractions` alone, keeping the concrete dispatcher implementation confined to your application/composition-root layer:

```bash
dotnet add package LoomKit.Notifications.Abstractions   # domain layer: define INotification/INotificationHandler
dotnet add package LoomKit.Notifications                # application layer: wire up the dispatcher
```

## Installation

### Via NuGet (recommended)

```bash
dotnet add package LoomKit.Notifications
```

Available on [nuget.org](https://www.nuget.org/packages/LoomKit.Notifications) — a package version is published automatically for every `vX.Y.Z` tag pushed to this repo.

If you'd rather build against the source directly instead (e.g. to track `main`, or to debug/modify the library alongside your app), two options:

### As a git submodule

```bash
git submodule add https://github.com/andreafoorlan/LoomKit.Notifications.git external/LoomKit.Notifications
cd external/LoomKit.Notifications
git checkout v1.0.0
cd ../..
git add external/LoomKit.Notifications
git commit -m "Add LoomKit.Notifications submodule pinned to v1.0.0"
```

Then reference the project from your solution/project:

```xml
<ProjectReference Include="..\external\LoomKit.Notifications\src\LoomKit.Notifications.csproj" />
```

When cloning a repository that already has this submodule:

```bash
git clone --recurse-submodules <your-repo-url>
# or, on an existing clone:
git submodule update --init --recursive
```

To move to a newer release later:

```bash
cd external/LoomKit.Notifications
git fetch --tags
git checkout v1.1.0
cd ../..
git add external/LoomKit.Notifications
git commit -m "Bump LoomKit.Notifications submodule to v1.1.0"
```

(`git submodule add -b <tag>` doesn't pin reliably since submodules track branches, not tags — `checkout` inside the submodule plus committing the resulting gitlink in the parent repo is what actually pins the commit.)

### Plain project reference

If you're vendoring the source directly instead of using a submodule:

```xml
<ProjectReference Include="..\path\to\LoomKit.Notifications\src\LoomKit.Notifications.csproj" />
```

## Core concepts

| Type | Purpose |
|---|---|
| `INotification` | Marker interface for a notification. |
| `INotificationHandler<TNotification>` | Implement one (or more!) per notification type — this is where the actual logic lives. Every handler registered for a type runs on dispatch. |
| `INotificationDispatcher` | The entry point your application code calls: `DispatchAsync(...)`. |
| `NotificationMiddleware<TNotification>` | Optional cross-cutting behavior wrapped around a handler (logging, validation, retries, ...). Applied independently around each matching handler. |

## Quick start

### 1. Define a notification and its handlers

```csharp
public sealed class UserRegistered : INotification
{
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
}

public sealed class SendWelcomeEmailHandler : INotificationHandler<UserRegistered>
{
    private readonly IEmailSender _emailSender;

    public SendWelcomeEmailHandler(IEmailSender emailSender) => _emailSender = emailSender;

    public Task HandleAsync(UserRegistered notification, CancellationToken cancellationToken = default)
        => _emailSender.SendWelcomeEmailAsync(notification.Email, cancellationToken);
}

public sealed class ProvisionDefaultWorkspaceHandler : INotificationHandler<UserRegistered>
{
    public Task HandleAsync(UserRegistered notification, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Provisioning default workspace for {notification.UserId}");
        return Task.CompletedTask;
    }
}
```

Both handlers run when `UserRegistered` is dispatched — unlike a request/response call, a notification isn't tied to a single handler.

### 2. Register handlers and the dispatcher in DI

Handlers are plain DI services. Register them one by one:

```csharp
services.AddScoped<INotificationHandler<UserRegistered>, SendWelcomeEmailHandler>();
services.AddScoped<INotificationHandler<UserRegistered>, ProvisionDefaultWorkspaceHandler>();

services.AddDefaultNotificationDispatcher(options => { });
```

...or scan one or more assemblies for every closed `INotificationHandler<>` implementation and register them all at once — no extra package required, this is built in:

```csharp
services.AddNotificationHandlersFromAssemblies(ServiceLifetime.Scoped, typeof(Program).Assembly);

// or across multiple assemblies, with whatever lifetime you need:
services.AddNotificationHandlersFromAssemblies(ServiceLifetime.Singleton, typeof(Program).Assembly, typeof(SomeOtherModule).Assembly);

services.AddDefaultNotificationDispatcher(options => { });
```

Calling it more than once, or passing overlapping assemblies, won't produce duplicate registrations. Note it only picks up **closed, concrete** handler classes — an open-generic handler (e.g. `class AuditHandler<TNotification> : INotificationHandler<TNotification>`) isn't discovered and must still be registered by hand: `services.AddScoped(typeof(INotificationHandler<>), typeof(AuditHandler<>));`.

`AddDefaultNotificationDispatcher` registers `INotificationDispatcher` (as `ServiceLifetime.Scoped` by default — override with `options.WithLifetime(...)`, see [Service lifetime](#service-lifetime) below) backed by `DefaultNotificationDispatcher`.

### 3. Dispatch notifications

```csharp
public sealed class RegistrationService(INotificationDispatcher notificationDispatcher)
{
    public async Task RunAsync(Guid userId, string email, CancellationToken cancellationToken)
    {
        await notificationDispatcher.DispatchAsync(
            new UserRegistered { UserId = userId, Email = email },
            cancellationToken);
    }
}
```

If no handler is registered for a notification type, `DispatchAsync` simply completes without doing anything — dispatching is fire-and-forget by design, there's no result to observe and no error for "nobody was listening".

## Middleware pipeline

A middleware wraps the next handler in the chain and decides whether/when to call it — same idea as ASP.NET Core middleware, but per notification type. Because a notification can have several handlers, the pipeline is built **independently for each one**: every handler gets wrapped by its own copy of the configured middlewares, in the same order.

> **You don't register middleware classes in DI.** The pipeline constructs them directly via `ActivatorUtilities`, passing the next handler explicitly and resolving any other constructor parameter (like `ILogger<>` below) from the container. All you register in DI are the middleware's *own* dependencies, if any — the middleware type itself is only ever passed to `UseNotificationMiddleware`, never to `services.Add...`.

```csharp
public sealed class LoggingMiddleware<TNotification> : NotificationMiddleware<TNotification>
    where TNotification : INotification
{
    private readonly ILogger<LoggingMiddleware<TNotification>> _logger;

    public LoggingMiddleware(INotificationHandler<TNotification> nextHandler, ILogger<LoggingMiddleware<TNotification>> logger)
        : base(nextHandler)
    {
        _logger = logger;
    }

    public override async Task HandleAsync(TNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling {NotificationType}", typeof(TNotification).Name);

        await _nextHandler.HandleAsync(notification, cancellationToken);

        _logger.LogInformation("Handled {NotificationType}", typeof(TNotification).Name);
    }
}
```

Register it as an **open generic type**:

```csharp
services.AddDefaultNotificationDispatcher(options => options
    .UseNotificationMiddleware(typeof(LoggingMiddleware<>)));
```

**Execution order:** middlewares run in the order they're registered — the first one registered is the outermost, so it runs first on the way in and last on the way out (a normal "onion" pipeline), and this holds separately for each of the notification's handlers:

```csharp
options
    .UseNotificationMiddleware(typeof(LoggingMiddleware<>))     // runs 1st, then last
    .UseNotificationMiddleware(typeof(ValidationMiddleware<>)); // runs 2nd, then first
```

`ClearNotificationMiddlewares()` resets the pipeline built so far if you need to override it conditionally.

## Service lifetime

`AddDefaultNotificationDispatcher` (and the generic `AddNotificationDispatcher`) registers the dispatcher as `ServiceLifetime.Scoped` by default. Override it with `WithLifetime(...)` in the options builder:

```csharp
services.AddDefaultNotificationDispatcher(options => options
    .WithLifetime(ServiceLifetime.Singleton));
```

⚠️ Only switch to `Singleton` if every handler (and every dependency each handler pulls in) is safe to resolve from the DI root container for the lifetime of the app. The dispatcher captures `IServiceProvider` once at construction and reuses it for every `DispatchAsync` call; if it's a singleton but a handler (or one of its dependencies, e.g. a scoped `DbContext`) is registered as `Scoped`, resolving it from the root provider either throws (`ValidateScopes` enabled) or silently creates a captive dependency shared across otherwise-unrelated calls. Stick with the `Scoped` default unless you know all your handlers are stateless/singleton-safe.

## Extensibility: custom dispatchers

`DefaultNotificationDispatcher` is just the built-in implementation. If you need different behavior at the dispatcher level (rather than as a middleware), derive from `NotificationDispatcher<TOptions>` and register it with the generic overload:

```csharp
services.AddNotificationDispatcher<MyNotificationDispatcher, MyNotificationDispatcherOptionsBuilder, MyNotificationDispatcherOptions>(options => { });
```

## Cancellation

`CancellationToken` passed to `DispatchAsync` flows through every middleware and reaches each handler's `HandleAsync`. Make sure any custom middleware you write forwards the token it receives to `_nextHandler.HandleAsync(notification, cancellationToken)` instead of dropping it.

## Observability

Every `DispatchAsync` call starts a `notification.dispatch {NotificationTypeName}` `Activity`, and a nested `notification.handle {NotificationTypeName}` activity per handler invoked, on an `ActivitySource` named after the assembly (`LoomKit.Notifications`), tagged with `notification.type` and, on the per-handler activity, `handler.type`. If a handler or middleware throws, the exception is recorded on that handler's activity via `Activity.AddException` (standard OpenTelemetry semantic conventions), including its message and stack trace — the exception then propagates out of `DispatchAsync`, so any handlers after the failing one in the fan-out are not invoked.

⚠️ If your tracing backend doesn't have the same access controls as your application logs, avoid throwing exceptions from handlers whose `Message` carries secrets or personal data — they will flow into your trace exporter as-is.

## License

[MIT](LICENSE)
