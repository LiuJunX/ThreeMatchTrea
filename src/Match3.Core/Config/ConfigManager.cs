using System;
using System.Collections.Generic;
using System.IO;

namespace Match3.Core.Config;

/// <summary>
/// Manages loading and accessing game configuration.
/// </summary>
public class ConfigManager
{
    private Dictionary<int, ItemConfig> _items;

    public bool IsLoaded { get; private set; }

    public void Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Config file not found", path);
        }

        // Use FileStream to read
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        using (var reader = new BinaryReader(fs))
        {
            // 1. Header Check
            var magic = reader.ReadChars(4);
            if (new string(magic) != "M3CF")
            {
                throw new InvalidDataException("Invalid config file format.");
            }

            var version = reader.ReadInt32();
            if (version != 1)
            {
                throw new InvalidDataException($"Unsupported config version: {version}");
            }

            // 2. Load Items
            int count = reader.ReadInt32();
            _items = new Dictionary<int, ItemConfig>(count);

            for (int i = 0; i < count; i++)
            {
                var item = new ItemConfig();
                item.Id = reader.ReadInt32();
                item.Name = reader.ReadString();
                item.Cost = reader.ReadInt32();
                item.Power = reader.ReadInt32();

                _items[item.Id] = item;
            }
        }

        IsLoaded = true;
    }

    public ItemConfig? GetItem(int id)
    {
        if (_items.TryGetValue(id, out var item))
        {
            return item;
        }
        return null;
    }

    public IEnumerable<ItemConfig> GetAllItems()
    {
        return _items?.Values ?? System.Linq.Enumerable.Empty<ItemConfig>();
    }
}
