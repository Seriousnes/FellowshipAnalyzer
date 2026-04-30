#:property PublishAot=false

using System.Text.Json;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: dotnet run src/FellowshipAnalyzer.Tools/event-schema.cs <log-json>");
    return 1;
}

var inputPath = Path.GetFullPath(args[0]);
if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Input file not found: {inputPath}");
    return 1;
}

await using var stream = File.OpenRead(inputPath);
using var document = await JsonDocument.ParseAsync(stream);

var eventArray = FindEventArray(document.RootElement);
if (eventArray is null)
{
    Console.Error.WriteLine(
        "Could not find an events array. Supported inputs are either a top-level event array or a raw report JSON containing data.reportData.report.events.data.");
    return 1;
}

var schemas = Analyze(eventArray.Value);
if (schemas.Count == 0)
{
    Console.WriteLine("# Event Schema Analysis");
    Console.WriteLine();
    Console.WriteLine($"Input: `{inputPath}`");
    Console.WriteLine();
    Console.WriteLine("No events found in the selected array.");
    return 0;
}

WriteMarkdown(inputPath, schemas);
return 0;

// ---- Analysis ----

static Dictionary<string, EventTypeSchema> Analyze(JsonElement eventArray)
{
    var schemas = new Dictionary<string, EventTypeSchema>(StringComparer.OrdinalIgnoreCase);

    foreach (var item in eventArray.EnumerateArray())
    {
        if (item.ValueKind != JsonValueKind.Object)
            continue;

        var eventType = "<missing>";
        if (item.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
            eventType = typeEl.GetString() ?? "<missing>";

        if (!schemas.TryGetValue(eventType, out var schema))
        {
            schema = new EventTypeSchema(eventType);
            schemas[eventType] = schema;
        }

        schema.ObserveEvent(item);
    }

    return schemas;
}

static void WriteMarkdown(string inputPath, Dictionary<string, EventTypeSchema> schemas)
{
    var sorted = schemas.Values.OrderByDescending(s => s.Count).ToList();
    var totalEvents = sorted.Sum(s => s.Count);

    Console.WriteLine("# Event Schema Analysis");
    Console.WriteLine();
    Console.WriteLine($"Input: `{inputPath}`");
    Console.WriteLine();
    Console.WriteLine($"Total events: {totalEvents}");
    Console.WriteLine($"Unique event types: {sorted.Count}");
    Console.WriteLine();

    Console.WriteLine("## Summary");
    Console.WriteLine();
    Console.WriteLine("| Event Type | Count | % of Total | Unique Properties |");
    Console.WriteLine("| --- | ---: | ---: | ---: |");
    foreach (var schema in sorted)
    {
        var pct = totalEvents > 0 ? $"{schema.Count * 100.0 / totalEvents:F1}%" : "—";
        Console.WriteLine($"| `{schema.EventType}` | {schema.Count} | {pct} | {schema.Properties.Count} |");
    }

    foreach (var schema in sorted)
    {
        Console.WriteLine();
        Console.WriteLine($"## `{schema.EventType}` ({schema.Count} events)");
        Console.WriteLine();
        Console.WriteLine("| Property | Frequency | JSON Type(s) | Notes |");
        Console.WriteLine("| --- | --- | --- | --- |");

        foreach (var (propName, propInfo) in schema.Properties.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            var frequency = FormatFrequency(propInfo.Count, schema.Count);
            var types = string.Join(", ", propInfo.ValueKinds.Order().Select(k => $"`{k}`"));
            var notes = propInfo.ChildProperties.Count > 0
                ? EscapePipes("children: " + string.Join(", ", propInfo.ChildProperties.Order(StringComparer.OrdinalIgnoreCase).Select(p => $"`{p}`")))
                : "";

            Console.WriteLine($"| `{propName}` | {frequency} | {types} | {notes} |");
        }
    }
}

static string FormatFrequency(int count, int total)
{
    if (total == 0) return "?";
    if (count == total) return "always";
    var pct = count * 100 / total;
    return $"{count}/{total} ({pct}%)";
}

static string EscapePipes(string value) => value.Replace("|", "\\|");

// ---- FindEventArray (shared pattern) ----

static JsonElement? FindEventArray(JsonElement root)
{
    if (TryGetNested(root, out var knownArray, "data", "reportData", "report", "events", "data") &&
        knownArray.ValueKind == JsonValueKind.Array)
    {
        return knownArray;
    }

    if (LooksLikeEventArray(root))
        return root;

    return FindEventArrayRecursive(root);
}

static JsonElement? FindEventArrayRecursive(JsonElement element)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            foreach (var property in element.EnumerateObject())
            {
                var match = FindEventArrayRecursive(property.Value);
                if (match is not null)
                    return match;
            }
            break;

        case JsonValueKind.Array:
            if (LooksLikeEventArray(element))
                return element;

            foreach (var item in element.EnumerateArray())
            {
                var match = FindEventArrayRecursive(item);
                if (match is not null)
                    return match;
            }
            break;
    }

    return null;
}

static bool LooksLikeEventArray(JsonElement element)
{
    if (element.ValueKind != JsonValueKind.Array)
        return false;

    var inspected = 0;
    var eventLike = 0;

    foreach (var item in element.EnumerateArray())
    {
        if (item.ValueKind != JsonValueKind.Object)
            continue;

        inspected++;

        if (item.TryGetProperty("type", out _) || item.TryGetProperty("ability", out _) || item.TryGetProperty("sourceID", out _))
            eventLike++;

        if (inspected >= 25)
            break;
    }

    if (inspected == 0)
        return false;

    return eventLike >= Math.Max(3, inspected / 2);
}

static bool TryGetNested(JsonElement element, out JsonElement current, params string[] path)
{
    current = element;
    foreach (var segment in path)
    {
        if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
        {
            current = default;
            return false;
        }
    }
    return true;
}

// ---- Types ----

sealed class EventTypeSchema(string eventType)
{
    public string EventType { get; } = eventType;
    public int Count { get; private set; }
    public SortedDictionary<string, PropertyInfo> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void ObserveEvent(JsonElement eventObject)
    {
        Count++;

        foreach (var property in eventObject.EnumerateObject())
        {
            if (!Properties.TryGetValue(property.Name, out var info))
            {
                info = new PropertyInfo();
                Properties[property.Name] = info;
            }

            info.Observe(property.Value);
        }
    }
}

sealed class PropertyInfo
{
    public int Count { get; private set; }
    public SortedSet<string> ValueKinds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public SortedSet<string> ChildProperties { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void Observe(JsonElement value)
    {
        Count++;

        // Normalize True/False to Boolean for clarity
        var kind = value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? "Boolean"
            : value.ValueKind.ToString();

        ValueKinds.Add(kind);

        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var child in value.EnumerateObject())
                ChildProperties.Add(child.Name);
        }
    }
}
