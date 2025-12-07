using FellowshipAnalyzer.Core.Common.Items;
using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Common;

/// <summary>
/// Seed registry with a known item for testing (GenericItems is currently empty).
/// </summary>
file class TestItems : IItemRegistry
{
    public static Item TestPotion { get; } = new(99001, "Test Potion", "T_Test_Potion.jpg");
    public static Item TestTrinket { get; } = new(99002, "Test Trinket", "T_Test_Trinket.jpg");
}

public class ItemRegistryTests
{
    public ItemRegistryTests()
    {
        // Ensure the test assembly (containing TestItems) is scanned.
        ItemRegistry.EnsureAssembly(typeof(TestItems).Assembly);
    }

    [Fact]
    public void Get_KnownItem_ReturnsCorrectItem()
    {
        var item = ItemRegistry.Get(TestItems.TestPotion.Id);

        Assert.Equal(TestItems.TestPotion.Id, item.Id);
        Assert.Equal(TestItems.TestPotion.Name, item.Name);
    }

    [Fact]
    public void Get_UnknownId_ThrowsKeyNotFoundException()
    {
        Assert.Throws<KeyNotFoundException>(() => ItemRegistry.Get(-99999));
    }

    [Fact]
    public void MaybeGet_KnownItem_ReturnsItem()
    {
        var item = ItemRegistry.MaybeGet(TestItems.TestTrinket.Id);

        Assert.NotNull(item);
        Assert.Equal(TestItems.TestTrinket.Name, item.Name);
    }

    [Fact]
    public void MaybeGet_UnknownId_ReturnsNull()
    {
        var item = ItemRegistry.MaybeGet(-99999);

        Assert.Null(item);
    }

    [Fact]
    public void TryGet_KnownItem_ReturnsTrueAndItem()
    {
        var found = ItemRegistry.TryGet(TestItems.TestPotion.Id, out var item);

        Assert.True(found);
        Assert.NotNull(item);
        Assert.Equal(TestItems.TestPotion.Name, item.Name);
    }

    [Fact]
    public void TryGet_UnknownId_ReturnsFalseAndNull()
    {
        var found = ItemRegistry.TryGet(-99999, out var item);

        Assert.False(found);
        Assert.Null(item);
    }

    [Fact]
    public void All_ContainsRegisteredItems()
    {
        var allIds = ItemRegistry.All.Keys;

        Assert.Contains(TestItems.TestPotion.Id, allIds);
        Assert.Contains(TestItems.TestTrinket.Id, allIds);
    }
}
