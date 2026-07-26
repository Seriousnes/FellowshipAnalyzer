using System.Text.RegularExpressions;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.UI;

/// <summary>
/// Guards the Timeline's cooldown-lane alignment contract, which lives entirely in markup and
/// stylesheets and so is invisible to every other test in this suite.
/// <para>
/// A cooldown row draws the sticky <c>.timeline-label</c> over the left end of a full-width
/// <c>.cooldown-lane</c> by floating the label. CSS only lets a float be overlapped by boxes in the
/// same block formatting context, so the moment the lane establishes one of its own the browser moves
/// it clear of the label and every cooldown icon and recharge bar is painted late by the label's width
/// (180px, three whole seconds at the default 60px per second), while the cast bar above, which has no
/// label, stays put. Adding <c>overflow: hidden</c> to the lane did exactly that.
/// </para>
/// </summary>
public sealed class TimelineLaneAlignmentTests
{
    private const string TimelineDirectory = "src/FellowshipAnalyzer.Core/UI/Timeline";

    /// <summary>
    /// Properties that make an element a block formatting context root, and so stop it overlapping a
    /// float. <c>position</c> is absent deliberately: the lane is <c>position: relative</c>, which
    /// establishes a containing block but not a formatting context.
    /// </summary>
    private static readonly (string Property, string Pattern)[] BlockFormattingContextTriggers =
    [
        ("overflow", @"^(?!visible\b|clip\b).+"),
        ("overflow-x", @"^(?!visible\b|clip\b).+"),
        ("overflow-y", @"^(?!visible\b|clip\b).+"),
        ("display", @"^(flow-root|inline-block|inline-flex|inline-grid|table|table-cell|table-caption|inline-table|grid|flex)\b"),
        ("contain", @"\b(layout|paint|content|strict)\b"),
        ("float", @"^(?!none\b).+"),
        ("column-count", @"^(?!auto\b).+"),
        ("column-width", @"^(?!auto\b).+"),
    ];

    [Fact]
    public void CooldownLane_DoesNotEstablishABlockFormattingContext_WhileTheLabelIsFloated()
    {
        if (!LabelIsFloated())
        {
            return;
        }

        var offenders = new List<string>();

        foreach (var file in TimelineStyleSheets())
        {
            var source = File.ReadAllText(file);

            foreach (var declaration in DeclarationsFor(source, ".cooldown-lane"))
            {
                foreach (var (property, pattern) in BlockFormattingContextTriggers)
                {
                    if (declaration.Property == property && Regex.IsMatch(declaration.Value, pattern, RegexOptions.IgnoreCase))
                    {
                        offenders.Add($"{Path.GetFileName(file)}: {declaration.Property}: {declaration.Value}");
                    }
                }
            }
        }

        offenders.ShouldBeEmpty(
            ".cooldown-lane must not establish a block formatting context while .timeline-label is a float: " +
            "the lane is pushed clear of the label and every cooldown icon and bar is drawn late by the " +
            "label's width. CooldownLaneModel already clips its geometry to the render window, so the lane " +
            "needs no overflow of its own.");
    }

    [Fact]
    public void CooldownLane_InlineStyleCarriesNoLayoutProperties()
    {
        var razor = File.ReadAllText(Path.Combine(TimelineRoot(), "CooldownLane.razor"));

        var inlineStyle = Regex.Match(razor, """class="cooldown-lane"\s+style="([^"]*)""" + "\"");
        inlineStyle.Success.ShouldBeTrue("CooldownLane.razor no longer renders a .cooldown-lane root with an inline style.");

        foreach (var declaration in ParseDeclarations(inlineStyle.Groups[1].Value))
        {
            foreach (var (property, pattern) in BlockFormattingContextTriggers)
            {
                if (declaration.Property == property)
                {
                    Regex.IsMatch(declaration.Value, pattern, RegexOptions.IgnoreCase).ShouldBeFalse(
                        $"CooldownLane.razor sets {declaration.Property}: {declaration.Value} inline, which makes the " +
                        "lane a block formatting context root and pushes it clear of the floated .timeline-label.");
                }
            }
        }
    }

    private static bool LabelIsFloated() =>
        DeclarationsFor(File.ReadAllText(Path.Combine(TimelineRoot(), "Timeline.razor.scss")), ".timeline-label")
            .Any(d => d.Property == "float" && !d.Value.Equals("none", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> TimelineStyleSheets() =>
        Directory.EnumerateFiles(TimelineRoot(), "*.scss");

    /// <summary>
    /// Yields the declarations every rule whose selector list names <paramref name="selector"/> applies
    /// directly to it. Nested rules are skipped: they style descendants, not the element itself.
    /// </summary>
    private static IEnumerable<(string Property, string Value)> DeclarationsFor(string scss, string selector)
    {
        foreach (Match rule in Regex.Matches(scss, @"(?<selector>[^{}();@]+?)\{(?<body>[^{}]*)\}"))
        {
            var selectors = rule.Groups["selector"].Value
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0);

            if (!selectors.Any(s => s == selector || s.StartsWith($"{selector}:", StringComparison.Ordinal)))
            {
                continue;
            }

            foreach (var declaration in ParseDeclarations(rule.Groups["body"].Value))
            {
                yield return declaration;
            }
        }
    }

    private static IEnumerable<(string Property, string Value)> ParseDeclarations(string body)
    {
        foreach (var statement in body.Split(';'))
        {
            var separator = statement.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var property = statement[..separator].Trim().ToLowerInvariant();
            var value = statement[(separator + 1)..].Trim();

            if (property.Length > 0 && value.Length > 0 && !property.StartsWith("--", StringComparison.Ordinal))
            {
                yield return (property, value);
            }
        }
    }

    private static string TimelineRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, TimelineDirectory.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not find {TimelineDirectory} above {AppContext.BaseDirectory}.");
    }
}
