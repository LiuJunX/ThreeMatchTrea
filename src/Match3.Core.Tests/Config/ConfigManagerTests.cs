using System.IO;
using Match3.Core.Config;
using Xunit;

namespace Match3.Core.Tests.Config;

public class ConfigManagerTests
{
    #region Helper Methods

    /// <summary>
    /// Creates a valid binary config stream with the M3CF header, version 1,
    /// and the specified items.
    /// </summary>
    private static MemoryStream CreateConfigStream(params (int id, string name, int cost, int power)[] items)
    {
        var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            // Header
            writer.Write("M3CF".ToCharArray());
            writer.Write(1); // version
            writer.Write(items.Length); // count

            foreach (var (id, name, cost, power) in items)
            {
                writer.Write(id);
                writer.Write(name);
                writer.Write(cost);
                writer.Write(power);
            }
        }

        ms.Position = 0;
        return ms;
    }

    /// <summary>
    /// Creates a stream with an invalid magic header.
    /// </summary>
    private static MemoryStream CreateInvalidMagicStream()
    {
        var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("XXXX".ToCharArray());
            writer.Write(1);
            writer.Write(0);
        }

        ms.Position = 0;
        return ms;
    }

    /// <summary>
    /// Creates a stream with an unsupported version number.
    /// </summary>
    private static MemoryStream CreateUnsupportedVersionStream()
    {
        var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("M3CF".ToCharArray());
            writer.Write(99); // unsupported version
            writer.Write(0);
        }

        ms.Position = 0;
        return ms;
    }

    #endregion

    #region IsLoaded Tests

    [Fact]
    public void IsLoaded_BeforeLoad_ReturnsFalse()
    {
        var manager = new ConfigManager();

        Assert.False(manager.IsLoaded);
    }

    [Fact]
    public void IsLoaded_AfterLoad_ReturnsTrue()
    {
        var manager = new ConfigManager();
        using var stream = CreateConfigStream();

        manager.Load(stream);

        Assert.True(manager.IsLoaded);
    }

    #endregion

    #region Load(Stream) Tests

    [Fact]
    public void Load_ValidEmptyConfig_Succeeds()
    {
        var manager = new ConfigManager();
        using var stream = CreateConfigStream();

        manager.Load(stream);

        Assert.True(manager.IsLoaded);
    }

    [Fact]
    public void Load_ValidConfigWithItems_LoadsAllItems()
    {
        var manager = new ConfigManager();
        using var stream = CreateConfigStream(
            (1, "Sword", 100, 50),
            (2, "Shield", 80, 30),
            (3, "Potion", 20, 10)
        );

        manager.Load(stream);

        Assert.True(manager.IsLoaded);
        Assert.Equal(3, manager.GetAllItems().Count());
    }

    [Fact]
    public void Load_InvalidMagicHeader_ThrowsInvalidDataException()
    {
        var manager = new ConfigManager();
        using var stream = CreateInvalidMagicStream();

        Assert.Throws<InvalidDataException>(() => manager.Load(stream));
    }

    [Fact]
    public void Load_UnsupportedVersion_ThrowsInvalidDataException()
    {
        var manager = new ConfigManager();
        using var stream = CreateUnsupportedVersionStream();

        Assert.Throws<InvalidDataException>(() => manager.Load(stream));
    }

    [Fact]
    public void Load_CalledTwice_ReplacesItems()
    {
        var manager = new ConfigManager();

        using var stream1 = CreateConfigStream(
            (1, "OldItem", 10, 5)
        );
        manager.Load(stream1);
        Assert.Equal(1, manager.GetAllItems().Count());

        using var stream2 = CreateConfigStream(
            (10, "NewA", 100, 50),
            (20, "NewB", 200, 60)
        );
        manager.Load(stream2);

        Assert.Equal(2, manager.GetAllItems().Count());
        Assert.Null(manager.GetItem(1)); // old item gone
        Assert.NotNull(manager.GetItem(10));
        Assert.NotNull(manager.GetItem(20));
    }

    #endregion

    #region GetItem Tests

    [Fact]
    public void GetItem_ValidId_ReturnsItem()
    {
        var manager = new ConfigManager();
        using var stream = CreateConfigStream(
            (42, "MagicRing", 500, 100)
        );
        manager.Load(stream);

        var item = manager.GetItem(42);

        Assert.NotNull(item);
        Assert.Equal(42, item.Value.Id);
        Assert.Equal("MagicRing", item.Value.Name);
        Assert.Equal(500, item.Value.Cost);
        Assert.Equal(100, item.Value.Power);
    }

    [Fact]
    public void GetItem_InvalidId_ReturnsNull()
    {
        var manager = new ConfigManager();
        using var stream = CreateConfigStream(
            (1, "Item1", 10, 5)
        );
        manager.Load(stream);

        var item = manager.GetItem(999);

        Assert.Null(item);
    }

    [Fact]
    public void GetItem_BeforeLoad_ReturnsNull()
    {
        var manager = new ConfigManager();

        var item = manager.GetItem(1);

        Assert.Null(item);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void GetItem_BoundaryIds_ReturnsNull(int id)
    {
        var manager = new ConfigManager();
        using var stream = CreateConfigStream(
            (1, "Item1", 10, 5)
        );
        manager.Load(stream);

        var item = manager.GetItem(id);

        Assert.Null(item);
    }

    #endregion

    #region GetAllItems Tests

    [Fact]
    public void GetAllItems_BeforeLoad_ReturnsEmpty()
    {
        var manager = new ConfigManager();

        var items = manager.GetAllItems();

        Assert.Empty(items);
    }

    [Fact]
    public void GetAllItems_AfterLoad_ReturnsAll()
    {
        var manager = new ConfigManager();
        using var stream = CreateConfigStream(
            (1, "A", 10, 1),
            (2, "B", 20, 2),
            (3, "C", 30, 3)
        );
        manager.Load(stream);

        var items = manager.GetAllItems().ToList();

        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void GetAllItems_ContainsCorrectData()
    {
        var manager = new ConfigManager();
        using var stream = CreateConfigStream(
            (5, "Gem", 150, 75)
        );
        manager.Load(stream);

        var items = manager.GetAllItems().ToList();

        Assert.Single(items);
        var item = items[0];
        Assert.Equal(5, item.Id);
        Assert.Equal("Gem", item.Name);
        Assert.Equal(150, item.Cost);
        Assert.Equal(75, item.Power);
    }

    [Fact]
    public void GetAllItems_EmptyConfig_ReturnsEmpty()
    {
        var manager = new ConfigManager();
        using var stream = CreateConfigStream();
        manager.Load(stream);

        var items = manager.GetAllItems();

        Assert.Empty(items);
    }

    #endregion

    #region Duplicate ID Tests

    [Fact]
    public void Load_DuplicateIds_LastOneWins()
    {
        var manager = new ConfigManager();
        using var stream = CreateConfigStream(
            (1, "First", 10, 5),
            (1, "Second", 20, 10)
        );
        manager.Load(stream);

        var item = manager.GetItem(1);

        Assert.NotNull(item);
        Assert.Equal("Second", item.Value.Name);
        Assert.Equal(20, item.Value.Cost);
    }

    #endregion
}
