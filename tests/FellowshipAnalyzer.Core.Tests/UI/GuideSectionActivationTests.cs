using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.UI.Components;
using FellowshipAnalyzer.Core.UI.Guides;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.UI;

public sealed class GuideSectionActivationTests
{
    [Fact]
    public async Task SectionWithAnActiveGuideRenders()
    {
        var html = await RenderAsync(builder =>
        {
            AddGuide<ActiveGuide>(builder, 0, "first");
            AddGuide<InactiveGuide>(builder, 2, "second");
        });

        html.ShouldNotContain("guide-top-section inactive");
        html.ShouldContain("<section>first</section>");
        html.ShouldNotContain("second");
    }

    [Fact]
    public async Task SectionWhereEveryGuideIsInactiveRendersHidden()
    {
        var html = await RenderAsync(builder =>
        {
            AddGuide<InactiveGuide>(builder, 0, "first");
            AddGuide<InactiveGuide>(builder, 2, "second");
        });

        html.ShouldContain("guide-top-section inactive");
    }

    [Fact]
    public async Task SectionWithNoGuideRenders()
    {
        var html = await RenderAsync(builder =>
        {
            builder.OpenElement(0, "p");
            builder.AddContent(1, "plain");
            builder.CloseElement();
        });

        html.ShouldNotContain("guide-top-section inactive");
        html.ShouldContain("<p>plain</p>");
    }

    private static void AddGuide<TGuide>(RenderTreeBuilder builder, int sequence, string marker)
        where TGuide : TestGuide
    {
        builder.OpenComponent<TGuide>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(TestGuide.Marker), marker);
        builder.CloseComponent();
    }

    private static async Task<string> RenderAsync(RenderFragment childContent)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(Section.Title)] = "Cooldowns",
            [nameof(Section.ChildContent)] = childContent,
        });

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<Section>(parameters);
            return output.ToHtmlString();
        });
    }

    private abstract class TestGuide : GuideComponent<CombatLogParser>
    {
        [Parameter] public string Marker { get; set; } = "";

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            base.BuildRenderTree(builder);
            builder.OpenElement(0, "section");
            builder.AddContent(1, Marker);
            builder.CloseElement();
        }
    }

    private sealed class ActiveGuide : TestGuide
    {
        protected override bool IsActive() => true;
    }

    private sealed class InactiveGuide : TestGuide
    {
        protected override bool IsActive() => false;
    }
}
