# Configuration switch skeleton

## Application (abstraction)

```csharp
namespace RutaProducto.Application.Abstractions.Thing;

public interface IThingPolicy
{
    bool Required { get; }
    bool Matches(string? value);
}
```

## Infrastructure (options)

```csharp
namespace RutaProducto.Infrastructure.Thing;

/// <summary>Setting under <c>Thing:</c>, validated at startup. Empty means off. Messages name the key, never the value.</summary>
public sealed class ThingOptions
{
    public const string ValueKey = "Thing:Value";
    public const int MinLength = 12;

    public string? Value { get; private set; }

    public static ThingOptions BindAndValidate(IConfiguration configuration)
    {
        var value = configuration[ValueKey]?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return new ThingOptions();
        }

        if (value.Length < MinLength)
        {
            throw new InvalidOperationException(
                $"'{ValueKey}' is too short: it needs at least {MinLength} characters. Set Thing__Value or leave it empty to turn it off.");
        }

        return new ThingOptions { Value = value };
    }
}
```

## Infrastructure (registration)

```csharp
public static class ThingServiceCollectionExtensions
{
    public static IServiceCollection AddThing(this IServiceCollection services, ThingOptions options)
    {
        services.AddSingleton<IThingPolicy>(new ThingPolicy(options.Value));
        return services;
    }
}

// DependencyInjection.cs
services.AddThing(ThingOptions.BindAndValidate(configuration));
```

## Command/handler integration

- Add the field to the command; override `ToString()` so it never prints the value.
- Return a dedicated status (append enum members at the end) and map it to a Spanish message in the page.
- Order inside the handler: captcha → this check → any account lookup.

## Tests (one class per concern, Theory where cases share a shape)

- Options: absent/blank → off; invalid → throws naming the key; message does not contain the value.
- Policy/behavior: off passes anything; on matches only the configured value; trim/case rules.
- Handler: rejected value creates nothing and calls no downstream service; ordering vs captcha.
- Command: `ToString()` leaks nothing.
- DI: registration follows the setting.
