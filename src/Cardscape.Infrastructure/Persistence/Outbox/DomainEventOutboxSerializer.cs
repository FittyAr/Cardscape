using System.Text.Json;
using System.Text.Json.Serialization;
using Cardscape.Domain.Common;

namespace Cardscape.Infrastructure.Persistence.Outbox;

internal static class DomainEventOutboxSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DomainScalarValueConverterFactory() }
    };
    private static readonly Dictionary<string, Type> EventTypes = typeof(IDomainEvent).Assembly
        .GetTypes()
        .Where(type => !type.IsAbstract && typeof(IDomainEvent).IsAssignableFrom(type))
        .ToDictionary(type => type.FullName!, StringComparer.Ordinal);

    public static (string Type, string Json) Serialize(IDomainEvent @event)
    {
        Type eventType = @event.GetType();
        return (eventType.FullName!, JsonSerializer.Serialize(@event, eventType, Options));
    }

    public static IDomainEvent Deserialize(string eventType, string payloadJson)
    {
        if (!EventTypes.TryGetValue(eventType, out Type? type))
        {
            throw new InvalidOperationException($"Unknown domain event type '{eventType}'.");
        }

        return (IDomainEvent)(JsonSerializer.Deserialize(payloadJson, type, Options)
            ?? throw new InvalidOperationException($"Domain event '{eventType}' deserialized to null."));
    }
}

internal sealed class DomainScalarValueConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (typeToConvert.Namespace?.StartsWith("Cardscape.Domain", StringComparison.Ordinal) != true)
        {
            return false;
        }

        return typeToConvert.GetProperty("Value") is not null
            && (typeToConvert.GetConstructors().Any(constructor => constructor.GetParameters().Length == 1)
                || FindFactory(typeToConvert) is not null);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(DomainScalarValueConverter<>).MakeGenericType(typeToConvert))!;

    internal static System.Reflection.MethodInfo? FindFactory(Type type) => type
        .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .FirstOrDefault(method =>
            method.Name is "Create" or "From"
            && method.GetParameters().Length == 1);
}

internal sealed class DomainScalarValueConverter<T> : JsonConverter<T>
{
    private static readonly System.Reflection.PropertyInfo ValueProperty =
        typeof(T).GetProperty("Value")
        ?? throw new InvalidOperationException($"{typeof(T).Name} has no Value property.");

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        object? scalar = JsonSerializer.Deserialize(ref reader, ValueProperty.PropertyType, options);
        System.Reflection.MethodInfo? factory = DomainScalarValueConverterFactory.FindFactory(typeof(T));
        if (factory is not null)
        {
            object result = factory.Invoke(null, [scalar])
                ?? throw new JsonException($"{typeof(T).Name}.{factory.Name} returned null.");
            if (factory.ReturnType == typeof(T))
            {
                return (T)result;
            }

            System.Reflection.PropertyInfo? isFailure = result.GetType().GetProperty("IsFailure");
            if (isFailure?.GetValue(result) is true)
            {
                throw new JsonException($"Stored {typeof(T).Name} value is invalid.");
            }

            return (T)(result.GetType().GetProperty("Value")?.GetValue(result)
                ?? throw new JsonException($"{factory.ReturnType.Name} has no value."));
        }

        return (T)(Activator.CreateInstance(typeof(T), scalar)
            ?? throw new JsonException($"Could not construct {typeof(T).Name}."));
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, ValueProperty.GetValue(value), ValueProperty.PropertyType, options);
}
