using System;
using System.Diagnostics;
using GeoWar.Simulation;

namespace GeoWar.Tools
{
    // Dünya ölçeğinde ekonomi hesabının süresini ölçer.
    public static class Program
    {
        private const int WarmupDays = 60;
        private const int MeasuredDays = 365;
        private const int FramesPerSecond = 60;
        private const int MaxDaysPerSecond = 3;

        private sealed class Scenario
        {
            public string Name;
            public int Countries;
            public int Cities;
            public int FacilitiesPerCity;
        }

        public static void Main()
        {
            EconomyDefinition definition = EconomyJsonLoader.Load();
            Console.WriteLine("Runtime: " + Environment.Version + ", " + Environment.ProcessorCount + " çekirdek (tek çekirdekle ölçülür)");
            Console.WriteLine();

            var scenarios = new[]
            {
                new Scenario { Name = "Normal", Countries = 200, Cities = 5000, FacilitiesPerCity = 4 },
                new Scenario { Name = "Yogun", Countries = 200, Cities = 5000, FacilitiesPerCity = 8 },
                new Scenario { Name = "En kotu", Countries = 200, Cities = 20000, FacilitiesPerCity = 8 }
            };

            foreach (Scenario scenario in scenarios)
            {
                Run(definition, scenario);
            }
        }

        private static void Run(EconomyDefinition definition, Scenario scenario)
        {
            var settings = new WorldGeneratorSettings
            {
                CountryCount = scenario.Countries,
                CityCount = scenario.Cities,
                FacilitiesPerCity = scenario.FacilitiesPerCity,
                Seed = 42
            };

            WorldState world = WorldGenerator.Create(definition, settings);
            for (int day = 0; day < WarmupDays; day++)
            {
                EconomySimulator.AdvanceDay(definition, world);
            }

            var samples = new double[MeasuredDays];
            double ticksToMs = 1000.0 / Stopwatch.Frequency;
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int day = 0; day < MeasuredDays; day++)
            {
                long start = Stopwatch.GetTimestamp();
                EconomySimulator.AdvanceDay(definition, world);
                samples[day] = (Stopwatch.GetTimestamp() - start) * ticksToMs;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Array.Sort(samples);

            double total = 0;
            foreach (double sample in samples)
            {
                total += sample;
            }

            double average = total / MeasuredDays;
            double p99 = samples[(int)(MeasuredDays * 0.99)];
            double worst = samples[MeasuredDays - 1];
            int framesPerDay = FramesPerSecond / MaxDaysPerSecond;

            int alarms = 0;
            for (int facility = 0; facility < world.FacilityCount; facility++)
            {
                alarms += world.FacilityAlarm[facility];
            }

            int built = world.CountBuiltFacilities();
            Console.WriteLine(scenario.Name + ": " + scenario.Countries + " ulke, " + world.CityCount + " sehir, " + built + " tesis, " + world.FacilityCount + " slot");
            Console.WriteLine("  Gun basina ortalama: " + average.ToString("F3") + " ms, p99: " + p99.ToString("F3") + " ms, en kotu: " + worst.ToString("F3") + " ms");
            Console.WriteLine("  3x hizda islemci kullanimi: %" + (average * MaxDaysPerSecond / 10).ToString("F2"));
            Console.WriteLine("  Karelere dagitilinca kare basina: " + (average / framesPerDay).ToString("F3") + " ms (" + framesPerDay + " kare)");
            Console.WriteLine("  Olcum sirasinda ayrilan bellek: " + allocated + " bayt");
            Console.WriteLine("  Alarmdaki tesis: %" + (alarms * 100.0 / built).ToString("F1"));
            Console.WriteLine();
        }
    }
}
