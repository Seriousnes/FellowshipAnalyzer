#:property PublishAot=false

using System.Text.Json;

var reportsDir = args.Length > 0 ? args[0] : "raw-reports";
int[] heroismIds = [1001350, 1002253, 1002277, 1002662];

Console.WriteLine("Measures whether a flat percentage haste buff adds to rating-derived haste or multiplies it.");
Console.WriteLine("Compares periodic tick intervals inside and outside Spirit of Heroism (+30% haste) windows;");
Console.WriteLine("tick timing is purely haste-scaled, unlike cast times, which carry player reaction latency.");
Console.WriteLine("The deciding check is the implied base tick: under the correct model the value derived from");
Console.WriteLine("inside the window matches the one derived from outside it.");
Console.WriteLine();

foreach (var file in Directory.GetFiles(reportsDir, "*.json").OrderBy(f => f))
{
    var stem = Path.GetFileNameWithoutExtension(file);
    var sIdx = stem.LastIndexOf("-s", StringComparison.Ordinal);
    if (sIdx < 0 || !int.TryParse(stem[(sIdx + 2)..], out var playerId)) continue;

    await using var stream = File.OpenRead(file);
    using var doc = await JsonDocument.ParseAsync(stream);
    if (!TryPath(doc.RootElement, out var report, "data", "reportData", "report")) continue;
    if (!TryPath(report, out var events, "events", "data")) continue;

    var abilityNames = new Dictionary<long, string>();
    if (TryPath(report, out var abilities, "masterData", "abilities"))
        foreach (var a in abilities.EnumerateArray())
            if (a.TryGetProperty("gameID", out var gid) && gid.TryGetInt64(out var id))
                abilityNames[id] = a.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : "";

    double? hasteRating = null;
    var windows = new List<(long Start, long End)>();
    long? openWindow = null;
    var lastTick = new Dictionary<(long Ability, long Target, long Instance), long>();
    var intervals = new List<(long Ability, long At, long Gap)>();

    foreach (var e in events.EnumerateArray())
    {
        var type = Str(e, "type");
        var ts = Num(e, "timestamp") ?? 0;

        if (type == "combatantinfo" && Num(e, "sourceID") == playerId)
        {
            hasteRating = Num(e, "haste");
            continue;
        }

        if (Num(e, "targetID") == playerId && Num(e, "abilityGameID") is { } aid && heroismIds.Contains((int)aid))
        {
            if (type is "applydebuff" or "applybuff") openWindow ??= ts;
            else if (type is "removedebuff" or "removebuff" && openWindow is { } start) { windows.Add((start, ts)); openWindow = null; }
            continue;
        }

        if (type != "damage" && type != "heal") continue;
        if (Num(e, "sourceID") != playerId) continue;
        if (!e.TryGetProperty("tick", out var tick) || tick.ValueKind != JsonValueKind.True) continue;

        var ability = Num(e, "abilityGameID");
        var target = Num(e, "targetID");
        if (ability is null || target is null) continue;
        var key = (ability.Value, target.Value, Num(e, "targetInstance") ?? 0);

        if (lastTick.TryGetValue(key, out var prev))
        {
            var gap = ts - prev;
            if (gap is > 200 and < 6000) intervals.Add((ability.Value, prev, gap));
        }
        lastTick[key] = ts;
    }

    if (hasteRating is null || windows.Count == 0 || intervals.Count == 0) continue;

    bool Inside(long t) => windows.Any(w => t >= w.Start && t <= w.End - 500);
    bool Outside(long t) => windows.All(w => t < w.Start - 500 || t > w.End + 500);

    var grouped = intervals
        .GroupBy(s => s.Ability)
        .Select(g => new
        {
            Ability = g.Key,
            Name = abilityNames.GetValueOrDefault(g.Key, ""),
            Out = g.Where(s => Outside(s.At)).Select(s => (double)s.Gap).ToArray(),
            In = g.Where(s => Inside(s.At)).Select(s => (double)s.Gap).ToArray(),
        })
        .Where(g => g.Out.Length >= 10 && g.In.Length >= 5)
        .ToArray();

    if (grouped.Length == 0) continue;

    var ratingPct = RatingToPercentage(hasteRating.Value);
    var additive = (1 + ratingPct + 0.30) / (1 + ratingPct);

    Console.WriteLine($"### {stem}  player={playerId}  hasteRating={hasteRating}  ratingPct={ratingPct:P3}  windows={windows.Count}");
    Console.WriteLine($"    predicted tick-gap ratio out/in:  additive={additive:F4}   multiplicative=1.3000");
    foreach (var g in grouped)
    {
        var outMode = Mode(g.Out);
        var inMode = Mode(g.In);
        Console.WriteLine($"    {g.Ability,8} {g.Name,-28} out n={g.Out.Length,-5} mode={outMode,6:F0} med={Median(g.Out),7:F1} | in n={g.In.Length,-4} mode={inMode,6:F0} med={Median(g.In),7:F1} | ratio(mode)={outMode / inMode:F4} ratio(med)={Median(g.Out) / Median(g.In):F4}");
        Console.WriteLine($"             implied base tick from out: additive={outMode * (1 + ratingPct):F0}ms   in-window additive={inMode * (1 + ratingPct + 0.30):F0}ms   in-window multiplicative={inMode * (1 + ratingPct) * 1.30:F0}ms");
    }
    Console.WriteLine();
}

return 0;

static double Mode(double[] values)
{
    var buckets = values.GroupBy(v => Math.Round(v / 25.0) * 25.0).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First();
    return buckets.Average();
}

static double Median(double[] values)
{
    var sorted = values.Order().ToArray();
    var mid = sorted.Length / 2;
    return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
}

static double RatingToPercentage(double rating)
{
    if (rating <= 0) return 0;
    var raw = rating * 0.16;
    var pct =
        Math.Min(raw, 10.0)
        + Math.Clamp(raw - 10.0, 0.0, 5.0) * 0.98
        + Math.Clamp(raw - 15.0, 0.0, 5.0) * 0.96
        + Math.Clamp(raw - 20.0, 0.0, 5.0) * 0.94
        + Math.Max(raw - 25.0, 0.0) * 0.92;
    return pct / 100.0;
}

static string Str(JsonElement e, string name) =>
    e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

static long? Num(JsonElement e, string name) =>
    e.TryGetProperty(name, out var v) && v.TryGetInt64(out var l) ? l : null;

static bool TryPath(JsonElement root, out JsonElement result, params string[] path)
{
    result = root;
    foreach (var seg in path)
    {
        if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty(seg, out var next)) { result = default; return false; }
        result = next;
    }
    return true;
}
