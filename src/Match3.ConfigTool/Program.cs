using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Match3.Core.Config;

namespace Match3.ConfigTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Config Build Process...");

            // 1. Fetch Data (Mock Feishu)
            var json = FeishuMock.FetchItemsSheet();
            Console.WriteLine("Fetched data from Feishu (Mock).");

            // 2. Parse Data
            var items = ParseItems(json);
            Console.WriteLine($"Parsed {items.Count} items.");

            // 3. Serialize to Binary
            string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.bin");
            // Also output to the project root or a known location for the game to load
            string gameConfigPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../config.bin"));
            
            Serialize(items, gameConfigPath);
            Console.WriteLine($"Config written to: {gameConfigPath}");
        }

        static List<ItemConfig> ParseItems(string json)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            // Feishu usually returns a specific JSON structure (code, msg, data -> items)
            // For this mock, we assume a direct array of objects for simplicity, 
            // or a simplified Feishu structure.
            
            // Let's assume the mock returns a list of dictionaries or objects.
            // We define a DTO for parsing.
            var dtos = JsonSerializer.Deserialize<List<ItemDto>>(json, options);
            
            var result = new List<ItemConfig>();
            foreach (var dto in dtos)
            {
                result.Add(new ItemConfig
                {
                    Id = dto.Id,
                    Name = dto.Name,
                    Cost = dto.Cost,
                    Power = dto.Power
                });
            }
            return result;
        }

        static void Serialize(List<ItemConfig> items, string path)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(fs))
            {
                // Header
                writer.Write("M3CF".ToCharArray()); // Magic: Match3 Config File
                writer.Write(1); // Version

                // Table: Items
                writer.Write(items.Count);
                foreach (var item in items)
                {
                    writer.Write(item.Id);
                    writer.Write(item.Name ?? "");
                    writer.Write(item.Cost);
                    writer.Write(item.Power);
                }
            }
        }
    }

    // DTO for JSON parsing
    class ItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Cost { get; set; }
        public int Power { get; set; }
    }

    static class FeishuMock
    {
        public static string FetchItemsSheet()
        {
            // Simulate a JSON response from Feishu Open API
            // In reality, you would use HttpClient to call https://open.feishu.cn/open-apis/bitable/v1/apps/:app_token/tables/:table_id/records
            var data = new List<object>
            {
                new { id = 101, name = "Sword", cost = 100, power = 10 },
                new { id = 102, name = "Shield", cost = 150, power = 5 },
                new { id = 103, name = "Potion", cost = 50, power = 0 },
                new { id = 104, name = "Magic Wand", cost = 300, power = 25 }
            };

            return JsonSerializer.Serialize(data);
        }
    }
}
