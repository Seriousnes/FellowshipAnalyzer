using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Core.UI.Components;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.UI;

public sealed class GuideComponentActivationTests
{
    [Fact]
    public async Task InactiveGuideRendersNothingWhileActiveSiblingsRender()
    {
        var html = await RenderAsync<Host>();

        html.ShouldBe("<div><section>first</section><section>third</section></div>");
    }

    [Fact]
    public async Task InactiveGuideRunsNoLifecycleMethod()
    {
        Probe.Initialized.Clear();

        await RenderAsync<Host>();

        Probe.Initialized.ShouldBe(["first", "third"]);
    }

    private static async Task<string> RenderAsync<TComponent>() where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>();
            return output.ToHtmlString();
        });
    }

    private static class Probe
    {
        public static List<string> Initialized { get; } = [];
    }

    private abstract class TestGuide : GuideComponent<CombatLogParser>
    {
        [Parameter] public string Marker { get; set; } = "";

        protected override void OnInitialized() => Probe.Initialized.Add(Marker);

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

    private sealed class Host : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");

            builder.OpenComponent<ActiveGuide>(1);
            builder.AddComponentParameter(2, nameof(TestGuide.Marker), "first");
            builder.CloseComponent();

            builder.OpenComponent<InactiveGuide>(3);
            builder.AddComponentParameter(4, nameof(TestGuide.Marker), "second");
            builder.CloseComponent();

            builder.OpenComponent<ActiveGuide>(5);
            builder.AddComponentParameter(6, nameof(TestGuide.Marker), "third");
            builder.CloseComponent();

            builder.CloseElement();
        }
    }
}
