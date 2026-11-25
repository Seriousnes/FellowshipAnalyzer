using System.Reflection;

using FellowshipAnalyzer.Core.Analysis;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public sealed class HeroSpellbookGlobalCooldownTests
{
    [Fact]
    public void EveryHeroSpellbook_ReadsItsGlobalCooldownsFromTheExport()
    {
        var heroAbilities = HeroAbilitiesTypes().ToList();

        heroAbilities.ShouldNotBeEmpty();
        foreach (var type in heroAbilities)
        {
            var abilities = (Abilities)Activator.CreateInstance(type)!;
            abilities.Spellbook()
                .Where(entry => entry.Gcd is not null)
                .Select(entry => entry.PrimarySpell.Name)
                .ShouldBeEmpty($"{type.FullName} sets Gcd");
        }
    }

    private static IEnumerable<Type> HeroAbilitiesTypes()
    {
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "FellowshipAnalyzer.Heroes.*.dll"))
            Assembly.LoadFrom(path);

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("FellowshipAnalyzer.Heroes.", StringComparison.Ordinal) == true)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsClass: true } && type.IsSubclassOf(typeof(Abilities)));
    }
}
