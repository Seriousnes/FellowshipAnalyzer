using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Serialization;

/// <summary>
/// Polymorphic JSON converter that handles serialization and deserialization of events
/// based on the "type" discriminator property.
/// Scans assemblies for subtypes of <typeparamref name="T"/> and maps their discriminator values.
/// </summary>
public sealed class FSLJsonConverter<T> : JsonConverter<T>
{
    private const string DiscriminatorPropName = "type";
    private readonly Dictionary<string, Type> _discriminatorToSubtype = [];

    /// <summary>
    /// Separate options used for writing that exclude this converter to avoid infinite recursion.
    /// </summary>
    private JsonSerializerOptions? _writeOptions;

    public FSLJsonConverter()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var subType in assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(T)) && !type.IsAbstract))
            {
                if (subType.GetCustomAttribute<FabricatedAttribute>() is { }) continue;
                var discriminator = subType.GetCustomAttribute<FSLEventDiscriminatorAttribute>()?.TypeDiscriminator;
                if (string.IsNullOrEmpty(discriminator))
                {
                    if (!subType.Name.EndsWith("Event"))
                    {
                        continue;
                    }
                    discriminator = subType.Name[..^5].ToLower();
                }
                _discriminatorToSubtype.TryAdd(discriminator, subType);
            }
        }
    }

    public override T Read(ref Utf8JsonReader reader, Type objectType, JsonSerializerOptions options)
    {
        var reader2 = reader;
        using var doc = JsonDocument.ParseValue(ref reader2);

        var root = doc.RootElement;
        var typeField = root.GetProperty(DiscriminatorPropName);

        if (typeField.GetString() is not { } typeName)
        {
            throw new JsonException(
                $"Could not find string property {DiscriminatorPropName} " +
                $"when trying to deserialize {typeof(T).Name}");
        }

        if (!_discriminatorToSubtype.TryGetValue(typeName, out var type))
        {
            throw new JsonException($"Unknown type: {typeName}");
        }

        return (T)JsonSerializer.Deserialize(ref reader, type, options)!;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        // Serialize as the concrete runtime type using options that exclude this converter
        // to avoid infinite recursion.
        var writeOptions = GetWriteOptions(options);
        JsonSerializer.Serialize(writer, value, value!.GetType(), writeOptions);
    }

    private JsonSerializerOptions GetWriteOptions(JsonSerializerOptions source)
    {
        if (_writeOptions is not null) return _writeOptions;

        var opts = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = source.DefaultIgnoreCondition,
            PropertyNamingPolicy = source.PropertyNamingPolicy,
            PropertyNameCaseInsensitive = source.PropertyNameCaseInsensitive,
        };

        foreach (var converter in source.Converters)
        {
            if (converter is not FSLJsonConverter<T>)
                opts.Converters.Add(converter);
        }

        _writeOptions = opts;
        return _writeOptions;
    }
}
