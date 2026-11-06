using System;
using System.IO;
using System.Text.Json;
using GeoWar.Simulation;

namespace GeoWar.Tools
{
    // Unity dışı araçlar için economy.json okuyucu.
    public static class EconomyJsonLoader
    {
        private const string RelativePath = "Assets/Data/Resources/economy.json";

        public static EconomyDefinition Load()
        {
            string json = File.ReadAllText(FindDataFile());
            var options = new JsonSerializerOptions { IncludeFields = true };
            EconomyData data = JsonSerializer.Deserialize<EconomyData>(json, options);
            return EconomyDefinition.Build(data);
        }

        private static string FindDataFile()
        {
            string directory = AppContext.BaseDirectory;
            while (directory != null)
            {
                string candidate = Path.Combine(directory, RelativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = Path.GetDirectoryName(directory);
            }

            throw new FileNotFoundException("Veri dosyası bulunamadı: " + RelativePath);
        }
    }
}
