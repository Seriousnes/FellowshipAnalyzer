using System.Text.Json.Serialization;

using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Serialization;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Serialization;

public class JsonContextSerializableRegistrationTests
{
    [Fact]
    public void AllConcreteEventSubtypes_AreRegisteredInFellowshipAnalyzerJsonContext()
    {
        var eventType = typeof(Event);
        var coreAssembly = eventType.Assembly;

        var concreteEventTypes = coreAssembly.GetTypes()
            .Where(t => !t.IsAbstract && eventType.IsAssignableFrom(t))
            .ToList();

        var registeredTypes = typeof(FellowshipAnalyzerJsonContext)
            .GetCustomAttributesData()
            .Where(a => a.AttributeType == typeof(JsonSerializableAttribute))
            .Select(a => (Type)a.ConstructorArguments[0].Value!)
            .ToHashSet();

        var missing = concreteEventTypes.Where(t => !registeredTypes.Contains(t)).ToList();

        Assert.True(
            missing.Count == 0,
            $"The following concrete Event subtypes are not registered in FellowshipAnalyzerJsonContext " +
            $"with [JsonSerializable(typeof(T))]. Add the missing attributes to prevent deserialization failures:\n" +
            string.Join("\n", missing.Select(t => $"  [JsonSerializable(typeof({t.Name}))]")));
    }
}
