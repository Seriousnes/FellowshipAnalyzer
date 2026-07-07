#:property PublishAot=false

using System.Text.Json;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: dotnet run src/FellowshipAnalyzer.Tools/resource-analysis.cs <log-json>");
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

var analysis = Analyze(eventArray.Value);
if (analysis.TotalEvents == 0)
{
    Console.WriteLine("# Resource Analysis");
    Console.WriteLine();
    Console.WriteLine($"Input: `{inputPath}`");
    Console.WriteLine();
    Console.WriteLine("No events found in the selected array.");
    return 0;
}

if (analysis.ResourceEventCount == 0)
{
    Console.WriteLine("# Resource Analysis");
    Console.WriteLine();
    Console.WriteLine($"Input: `{inputPath}`");
    Console.WriteLine();
    Console.WriteLine($"Events scanned: {analysis.TotalEvents}");
    Console.WriteLine();
    Console.WriteLine("No `sourceResources.resources` objects were found.");
    return 0;
}

WriteMarkdown(inputPath, analysis);
return 0;

static AnalysisResult Analyze(JsonElement eventArray)
{
    var totalEvents = 0;
    var resourceEventCount = 0;
    var summaries = new Dictionary<int, ResourceSummary>();

    foreach (var item in eventArray.EnumerateArray())
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            continue;
        }

        totalEvents++;

        if (!item.TryGetProperty("sourceResources", out var sourceResources) ||
            sourceResources.ValueKind != JsonValueKind.Object ||
            !sourceResources.TryGetProperty("resources", out var resources) ||
            resources.ValueKind != JsonValueKind.Object ||
            !resources.TryGetProperty("type", out var typeProperty) ||
            typeProperty.ValueKind != JsonValueKind.Number)
        {
            continue;
        }

        resourceEventCount++;

        var type = typeProperty.GetInt32();
        if (!summaries.TryGetValue(type, out var summary))
        {
            summary = new ResourceSummary(type);
            summaries[type] = summary;
        }

        var sourceId = TryGetInt(item, "sourceID");
        var eventType = TryGetString(item, "type") ?? "<missing>";
        var abilityName = TryGetAbilityName(item);
        var amount = TryGetInt(resources, "amount");
        var max = TryGetInt(resources, "max");

        summary.Observe(sourceId, eventType, abilityName, amount, max);
    }

    return new AnalysisResult(totalEvents, resourceEventCount, summaries.Values.OrderBy(summary => summary.Type).ToList());
}

static void WriteMarkdown(string inputPath, AnalysisResult analysis)
{
    Console.WriteLine("# Resource Analysis");
    Console.WriteLine();
    Console.WriteLine($"Input: `{inputPath}`");
    Console.WriteLine();
    Console.WriteLine($"Events scanned: {analysis.TotalEvents}");
    Console.WriteLine($"Events with `sourceResources.resources`: {analysis.ResourceEventCount}");
    Console.WriteLine($"Unique resource types: {analysis.Summaries.Count}");
    Console.WriteLine();
    Console.WriteLine("| Type | Occurrences | Amount Range | Max Range | Typical Event Pairings | Change Pattern |");
    Console.WriteLine("| --- | ---: | --- | --- | --- | --- |");

    foreach (var summary in analysis.Summaries)
    {
        Console.WriteLine(
            $"| `{summary.Type}` | {summary.Occurrences} | {summary.FormatAmountRange()} | {summary.FormatMaxRange()} | {EscapePipes(summary.TopEventAbilityPairs(3))} | increase {summary.IncreaseCount}, decrease {summary.DecreaseCount}, unchanged {summary.UnchangedCount} |"
        );
    }

    foreach (var summary in analysis.Summaries)
    {
        Console.WriteLine();
        Console.WriteLine($"## Type {summary.Type}");
        Console.WriteLine();
        Console.WriteLine($"- Occurrences: {summary.Occurrences}");
        Console.WriteLine($"- Amounts: unique {summary.UniqueAmountCount}, range {summary.FormatAmountRange()}");
        Console.WriteLine($"- Max values: unique {summary.UniqueMaxCount}, range {summary.FormatMaxRange()}");
        Console.WriteLine($"- Most common event types: {summary.TopEventTypes(8)}");
        Console.WriteLine($"- Most common abilities: {summary.TopAbilities(8)}");
        Console.WriteLine($"- Most common event+ability pairs: {summary.TopEventAbilityPairs(10)}");
        Console.WriteLine($"- Change counts: increase {summary.IncreaseCount}, decrease {summary.DecreaseCount}, unchanged {summary.UnchangedCount}");
        Console.WriteLine($"- Most common amount values: {summary.TopAmountValues(10)}");
        Console.WriteLine($"- Most common amount deltas: {summary.TopAmountDeltas(10)}");
        Console.WriteLine($"- Top increase pairs: {summary.TopIncreasePairs(8)}");
        Console.WriteLine($"- Top decrease pairs: {summary.TopDecreasePairs(8)}");
        Console.WriteLine($"- Top non-zero transitions: {summary.TopTransitions(8)}");
    }
}

static string EscapePipes(string value) => value.Replace("|", "\\|");

static JsonElement? FindEventArray(JsonElement root)
{
    if (TryGetNested(root, out var knownArray, "data", "reportData", "report", "events", "data") &&
        knownArray.ValueKind == JsonValueKind.Array)
    {
        return knownArray;
    }

    if (LooksLikeEventArray(root))
    {
        return root;
    }

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
                {
                    return match;
                }
            }
            break;

        case JsonValueKind.Array:
            if (LooksLikeEventArray(element))
            {
                return element;
            }

            foreach (var item in element.EnumerateArray())
            {
                var match = FindEventArrayRecursive(item);
                if (match is not null)
                {
                    return match;
                }
            }
            break;
    }

    return null;
}

static bool LooksLikeEventArray(JsonElement element)
{
    if (element.ValueKind != JsonValueKind.Array)
    {
        return false;
    }

    var inspected = 0;
    var eventLike = 0;
    var resourceLike = 0;

    foreach (var item in element.EnumerateArray())
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            continue;
        }

        inspected++;

        if (item.TryGetProperty("type", out _) || item.TryGetProperty("ability", out _) || item.TryGetProperty("sourceID", out _))
        {
            eventLike++;
        }

        if (item.TryGetProperty("sourceResources", out var sourceResources) &&
            sourceResources.ValueKind == JsonValueKind.Object &&
            sourceResources.TryGetProperty("resources", out var resources) &&
            resources.ValueKind == JsonValueKind.Object &&
            resources.TryGetProperty("type", out _))
        {
            resourceLike++;
        }

        if (inspected >= 25)
        {
            break;
        }
    }

    if (inspected == 0)
    {
        return false;
    }

    return resourceLike > 0 || eventLike >= Math.Max(3, inspected / 2);
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

static int? TryGetInt(JsonElement element, string propertyName)
    => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
        ? property.GetInt32()
        : null;

static string? TryGetString(JsonElement element, string propertyName)
    => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
        ? property.GetString()
        : null;

static string TryGetAbilityName(JsonElement item)
{
    if (item.TryGetProperty("ability", out var ability) &&
        ability.ValueKind == JsonValueKind.Object &&
        ability.TryGetProperty("name", out var nameProperty) &&
        nameProperty.ValueKind == JsonValueKind.String)
    {
        return nameProperty.GetString() ?? "<missing>";
    }

    return "<missing>";
}

sealed class ResourceSummary
{
    private readonly Dictionary<string, int> eventCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> abilityCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> pairCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, int> amountCounts = new();
    private readonly Dictionary<int, int> deltaCounts = new();
    private readonly Dictionary<string, int> increasePairs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> decreasePairs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> transitionCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, int> lastAmountBySource = new();
    private readonly HashSet<int> uniqueAmounts = new();
    private readonly HashSet<int> uniqueMaxValues = new();

    private int? minAmount;
    private int? maxAmount;
    private int? minMaxValue;
    private int? maxMaxValue;

    public ResourceSummary(int type)
    {
        Type = type;
    }

    public int Type { get; }
    public int Occurrences { get; private set; }
    public int IncreaseCount { get; private set; }
    public int DecreaseCount { get; private set; }
    public int UnchangedCount { get; private set; }
    public int UniqueAmountCount => uniqueAmounts.Count;
    public int UniqueMaxCount => uniqueMaxValues.Count;

    public void Observe(int? sourceId, string eventType, string abilityName, int? amount, int? max)
    {
        Occurrences++;
        Increment(eventCounts, eventType);
        Increment(abilityCounts, abilityName);
        Increment(pairCounts, $"{eventType} | {abilityName}");

        if (amount is int amountValue)
        {
            uniqueAmounts.Add(amountValue);
            Increment(amountCounts, amountValue);
            minAmount = minAmount is null ? amountValue : Math.Min(minAmount.Value, amountValue);
            maxAmount = maxAmount is null ? amountValue : Math.Max(maxAmount.Value, amountValue);

            if (sourceId is int sourceKey && lastAmountBySource.TryGetValue(sourceKey, out var previousAmount))
            {
                var delta = amountValue - previousAmount;
                if (delta > 0)
                {
                    IncreaseCount++;
                    Increment(increasePairs, $"{eventType} | {abilityName}");
                }
                else if (delta < 0)
                {
                    DecreaseCount++;
                    Increment(decreasePairs, $"{eventType} | {abilityName}");
                }
                else
                {
                    UnchangedCount++;
                }

                Increment(deltaCounts, delta);
                if (delta != 0)
                {
                    Increment(transitionCounts, $"{previousAmount}->{amountValue}");
                }
            }

            if (sourceId is int source)
            {
                lastAmountBySource[source] = amountValue;
            }
        }

        if (max is int maxValue)
        {
            uniqueMaxValues.Add(maxValue);
            minMaxValue = minMaxValue is null ? maxValue : Math.Min(minMaxValue.Value, maxValue);
            maxMaxValue = maxMaxValue is null ? maxValue : Math.Max(maxMaxValue.Value, maxValue);
        }
    }

    public string FormatAmountRange() => FormatRange(minAmount, maxAmount);
    public string FormatMaxRange() => FormatRange(minMaxValue, maxMaxValue);
    public string TopEventTypes(int take) => FormatTop(eventCounts, take);
    public string TopAbilities(int take) => FormatTop(abilityCounts, take);
    public string TopEventAbilityPairs(int take) => FormatTop(pairCounts, take);
    public string TopAmountValues(int take) => FormatTop(amountCounts, take);
    public string TopAmountDeltas(int take) => FormatTop(deltaCounts, take);
    public string TopIncreasePairs(int take) => FormatTop(increasePairs, take);
    public string TopDecreasePairs(int take) => FormatTop(decreasePairs, take);
    public string TopTransitions(int take) => FormatTop(transitionCounts, take);

    private static string FormatRange(int? min, int? max)
        => min is null || max is null ? "<missing>" : min == max ? min.Value.ToString() : $"{min} to {max}";

    private static void Increment(Dictionary<string, int> dictionary, string key)
    {
        if (dictionary.TryGetValue(key, out var count))
        {
            dictionary[key] = count + 1;
            return;
        }

        dictionary[key] = 1;
    }

    private static void Increment(Dictionary<int, int> dictionary, int key)
    {
        if (dictionary.TryGetValue(key, out var count))
        {
            dictionary[key] = count + 1;
            return;
        }

        dictionary[key] = 1;
    }

    private static string FormatTop(Dictionary<string, int> dictionary, int take)
        => dictionary.Count == 0
            ? "<none>"
            : string.Join(", ", dictionary.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key).Take(take).Select(entry => $"`{entry.Key}` ({entry.Value})"));

    private static string FormatTop(Dictionary<int, int> dictionary, int take)
        => dictionary.Count == 0
            ? "<none>"
            : string.Join(", ", dictionary.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key).Take(take).Select(entry => $"`{entry.Key}` ({entry.Value})"));
}

sealed record AnalysisResult(int TotalEvents, int ResourceEventCount, IReadOnlyList<ResourceSummary> Summaries);