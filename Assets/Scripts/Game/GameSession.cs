using System;
using System.Collections.Generic;
using System.IO;
using GeoWar.Map;
using GeoWar.Simulation;
using UnityEngine;

namespace GeoWar.Game
{
    // Oyunun durumu: harita, ekonomi ve oyuncunun ülkesi. Arayüz durumu buradan okur, değişikliği buradan ister.
    // Haritadaki her il bir şehirdir: il ve şehir indeksi aynıdır. Şehir nüfusları gerçek veriden gelir; tesisler ve rezervler şimdilik örnek veriyle üretilir.
    public sealed class GameSession
    {
        private const string MapResource = "map_region";
        private const string EconomyResource = "economy";
        private const string UiResource = "ui";
        private const string PopulationResource = "population";
        private const int PercentDivisor = 10;
        private const int WorldSeed = 1;
        private const int SampleFacilitiesPerCity = 2;
        private const int SampleMaxStartingLevel = 2;
        private const long SampleStartingStockUnits = 5000;
        private const long SampleStartingMoneyMilli = 500000000;

        public readonly MapData Map;
        public readonly EconomyDefinition Definition;
        public readonly WorldState World;
        public readonly UiData Ui;
        public readonly IconLibrary Icons;
        public readonly int Player;
        public readonly CountryRelation[] Relations;

        // İlin bugünkü sahibi. Haritadaki ülke oyun başındaki sahiptir; isyanla değişebilir.
        public readonly int[] ProvinceOwner;
        public readonly bool[] ProvinceAlarm;
        public readonly bool[] ProvinceReady;
        public readonly bool[] ProvinceRebellion;

        // Oyuncunun isyan eden şehirleri, süresi en az kalandan başlayarak.
        public readonly List<int> RebellionCities = new List<int>();
        public readonly BuildingViewData[] BuildingViews;
        public readonly ResourceViewData[] ResourceViews;
        public readonly ResourceBar Bar;

        private readonly SimulationClock _clock;
        private readonly DateTime _startDate;
        private readonly string[] _countryTitles;
        private readonly string[] _transportNames;
        private long _moneyAtDayStart;
        private int _seenOwnersVersion;
        private int _seenRebellions;
        private int _seenLostCities;
        private bool _ownersChanged;

        public long MoneyChangePerDay { get; private set; }
        public int AlarmProvinceCount { get; private set; }
        public int ReadyProvinceCount { get; private set; }

        // Şirket teklifi mekaniği henüz yok.
        public int OfferCount
        {
            get { return 0; }
        }

        public int Speed
        {
            get { return _clock.Speed; }
        }

        public float DayProgress
        {
            get { return _clock.DayProgress(World); }
        }

        private GameSession(MapData map, EconomyDefinition definition, UiData ui, PopulationData population)
        {
            Map = map;
            Definition = definition;
            Ui = ui;
            Icons = new IconLibrary(ui);
            Relations = BuildRelations(map, ui, definition.Balance.RebelStateSlots);
            Player = Array.IndexOf(map.Countries, ui.PlayerCountry);
            ProvinceOwner = StartingOwners(map);
            ProvinceAlarm = new bool[map.ProvinceCount];
            ProvinceReady = new bool[map.ProvinceCount];
            ProvinceRebellion = new bool[map.ProvinceCount];
            BuildingViews = MapBuildingViews(definition, ui);
            ResourceViews = MapResourceViews(definition, ui);
            Bar = new ResourceBar(MapStartBar(definition, ui));

            int[] citiesPerCountry = CountCities(map);
            World = WorldGenerator.Create(definition, new WorldGeneratorSettings
            {
                CitiesPerCountry = citiesPerCountry,
                CityPopulations = PopulationTable.ForCities(population, map.ProvinceIds, Sum(citiesPerCountry)),
                Seed = WorldSeed,
                FacilitiesPerCity = SampleFacilitiesPerCity,
                MaxStartingLevel = SampleMaxStartingLevel,
                StartingStockUnits = SampleStartingStockUnits,
                StartingMoney = SampleStartingMoneyMilli
            });
            MarkCoastalCities();

            // Oyuncu pazarda yalnızca elle alıp satar, isyana kendisi cevap verir; AI ülkeleri otomatik devam eder.
            World.CountryManualTrade[Player] = true;
            World.CountryPlayerControlled[Player] = true;
            _countryTitles = new string[World.CountryCount];
            _transportNames = MapTransportNames(definition, ui);
            ApplyCapitals();
            ApplyBordersAndWars();

            _startDate = new DateTime(ui.StartYear, ui.StartMonth, ui.StartDay);
            _moneyAtDayStart = World.Money[Player];
            _clock = new SimulationClock(definition.Balance.SecondsPerDay) { Speed = 1 };
            RefreshDerived();
        }

        public static GameSession Load()
        {
            MapData map = LoadMap();
            var definition = EconomyDefinition.Build(JsonUtility.FromJson<EconomyData>(Resources.Load<TextAsset>(EconomyResource).text));
            var population = JsonUtility.FromJson<PopulationData>(Resources.Load<TextAsset>(PopulationResource).text);
            return new GameSession(map, definition, LoadUi(), population);
        }

        public static MapData LoadMap()
        {
            using (var stream = new MemoryStream(Resources.Load<TextAsset>(MapResource).bytes))
            {
                return MapData.ReadFrom(stream);
            }
        }

        public static UiData LoadUi()
        {
            return JsonUtility.FromJson<UiData>(Resources.Load<TextAsset>(UiResource).text);
        }

        // Sonda isyancı devlet yerleri için de yer ayrılır; devlet kurulunca ilişkisi yazılır.
        public static CountryRelation[] BuildRelations(MapData map, UiData ui, int rebelStateSlots = 0)
        {
            var relations = new CountryRelation[map.Countries.Length + rebelStateSlots];
            for (int i = 0; i < relations.Length; i++)
            {
                relations[i] = CountryRelation.Peace;
                foreach (CountryViewData country in ui.Countries)
                {
                    if (i < map.Countries.Length && country.Key == map.Countries[i])
                    {
                        relations[i] = ParseRelation(country.Relation);
                    }
                }
            }

            return relations;
        }

        public static int[] StartingOwners(MapData map)
        {
            var owners = new int[map.ProvinceCount];
            for (int province = 0; province < owners.Length; province++)
            {
                owners[province] = map.ProvinceCountry[province];
            }

            return owners;
        }

        // Biten gün sayısını döndürür.
        public int Advance(float seconds)
        {
            int finishedDays = _clock.Advance(Definition, World, seconds);
            if (finishedDays > 0)
            {
                long money = World.Money[Player];
                MoneyChangePerDay = (money - _moneyAtDayStart) / finishedDays;
                _moneyAtDayStart = money;
                ApplyWorldChanges();
            }

            return finishedDays;
        }

        public void SetSpeed(int speed)
        {
            _clock.Speed = speed;
        }

        public bool IsOwnProvince(int province)
        {
            return ProvinceOwner[province] == Player;
        }

        public CountryViewData CountryView(int province)
        {
            return CountryViewAt(ProvinceOwner[province]);
        }

        // İsyancı devletin veri dosyasında görünümü yoktur.
        public CountryViewData CountryViewAt(int country)
        {
            if (country >= Map.Countries.Length)
            {
                return null;
            }

            string key = Map.Countries[country];
            foreach (CountryViewData view in Ui.Countries)
            {
                if (view.Key == key)
                {
                    return view;
                }
            }

            return null;
        }

        public string CountryKey(int country)
        {
            return country < Map.Countries.Length ? Map.Countries[country] : Ui.RebelFlag;
        }

        public FlagData CountryFlag(int country)
        {
            string key = CountryKey(country);
            foreach (FlagData flag in Ui.Flags)
            {
                if (flag.Key == key)
                {
                    return flag;
                }
            }

            return null;
        }

        public bool IsCapital(int province)
        {
            CountryViewData country = CountryView(province);
            return country != null && country.Capital == Map.ProvinceIds[province];
        }

        public string CapitalName(int country)
        {
            if (Rebellions.IsRebelState(World, country))
            {
                return Map.ProvinceNames[World.RebelCapital[country]];
            }

            CountryViewData view = CountryViewAt(country);
            int capital = view != null ? Array.IndexOf(Map.ProvinceIds, view.Capital) : -1;
            return capital >= 0 ? Map.ProvinceNames[capital] : "";
        }

        // İttifak şimdilik yalnızca oyuncuyla kurulur: oyuncunun müttefikleri veya müttefikse oyuncu.
        public void CollectAllies(int country, List<int> allies)
        {
            allies.Clear();
            if (country != Player)
            {
                if (Relations[country] == CountryRelation.Ally)
                {
                    allies.Add(Player);
                }

                return;
            }

            for (int other = 0; other < Relations.Length; other++)
            {
                if (Relations[other] == CountryRelation.Ally)
                {
                    allies.Add(other);
                }
            }
        }

        // Kurulan tesisin slotunu döndürür; kurulamazsa -1.
        public int Build(int city, int building)
        {
            int slot = IsOwnProvince(city) ? FacilityCommands.TryBuild(Definition, World, Player, city, building) : -1;
            RefreshDerived();
            return slot;
        }

        public bool Upgrade(int slot)
        {
            bool started = IsOwnProvince(World.FacilityCity[slot]) && FacilityCommands.TryUpgrade(Definition, World, Player, slot);
            RefreshDerived();
            return started;
        }

        // Tek şehre veya tüm isyanlara cevap verir. Cevap verilen şehir sayısını döndürür.
        public int AnswerRebellion(int city, RebellionResponse response)
        {
            int answered = Rebellions.TryAnswer(Definition, World, city, response) ? 1 : 0;
            RefreshDerived();
            return answered;
        }

        public int AnswerAllRebellions(RebellionResponse response)
        {
            int answered = Rebellions.TryAnswerAll(Definition, World, Player, response);
            RefreshDerived();
            return answered;
        }

        // Görünüm sorar: şehir el değiştirdiyse haritayı yeniler.
        public bool TakeOwnersChange()
        {
            bool changed = _ownersChanged;
            _ownersChanged = false;
            return changed;
        }

        public bool TakeNewRebellion()
        {
            int started = World.CountryRebellionsStarted[Player];
            bool isNew = started != _seenRebellions;
            _seenRebellions = started;
            return isNew;
        }

        // Son sorudan beri isyanla kaybedilen şehir sayısı.
        public int TakeLostCities()
        {
            int lost = World.CountryCitiesLost[Player];
            int change = lost - _seenLostCities;
            _seenLostCities = lost;
            return change;
        }

        // Otomatik test bu ekranı açar: oyuncunun ilk şehirlerinde isyan başlatır.
        public void StartRebellionsForTest(int count)
        {
            int start = World.CountryCityStart[Player];
            for (int i = 0; i < count; i++)
            {
                Rebellions.Start(Definition, World, Player, World.CountryCities[start + i]);
            }

            ApplyWorldChanges();
        }

        // Otomatik test bu ekranı açar: oyuncunun ilk şehirleri isyancı devlete geçer.
        public void LoseCitiesForTest(int count)
        {
            int start = World.CountryCityStart[Player];
            for (int i = 0; i < count; i++)
            {
                int city = World.CountryCities[start + i];
                Rebellions.Start(Definition, World, Player, city);
                World.CityRebellionDays[city] = 1;
            }

            Rebellions.AdvanceDay(Definition, World);
            ApplyWorldChanges();
        }

        public bool BuyOffer(int seller, int resource, long amountMilli)
        {
            return MarketCommands.TryBuy(Definition, World, Player, seller, resource, amountMilli);
        }

        public bool ListForSale(int resource, long amountMilli)
        {
            return MarketCommands.TryList(World, Player, resource, amountMilli);
        }

        public bool WithdrawOffer(int resource)
        {
            return MarketCommands.TryWithdraw(World, Player, resource);
        }

        public long PlayerOfferMilli(int resource)
        {
            return MarketCommands.OfferMilli(World, Player, resource);
        }

        public string CountryTitle(int country)
        {
            return _countryTitles[country];
        }

        public string TransportName(int transport)
        {
            return _transportNames[transport];
        }

        public long PlayerStock(int resource)
        {
            return World.Stock[Player * World.ResourceCount + resource] / EconomyDefinition.MilliPerUnit;
        }

        public long PlayerStockMilli(int resource)
        {
            return World.Stock[Player * World.ResourceCount + resource];
        }

        public long PlayerProducedMilli(int resource)
        {
            return World.DailyProduced[Player * World.ResourceCount + resource];
        }

        public long PlayerConsumedMilli(int resource)
        {
            return World.DailyConsumed[Player * World.ResourceCount + resource];
        }

        public long PlayerMoney()
        {
            return World.Money[Player] / EconomyDefinition.MilliPerUnit;
        }

        public int PlayerSatisfactionPercent()
        {
            int start = World.CountryCityStart[Player];
            int end = World.CountryCityStart[Player + 1];
            long total = 0;
            for (int i = start; i < end; i++)
            {
                total += World.Satisfaction[World.CountryCities[i]];
            }

            return end > start ? (int)(total / (end - start) / PercentDivisor) : 0;
        }

        public int PlayerEnergyPercent()
        {
            return World.EnergyRatioPermille[Player] / PercentDivisor;
        }

        public int CitySatisfactionPercent(int city)
        {
            return World.Satisfaction[city] / PercentDivisor;
        }

        public string DateText()
        {
            DateTime date = _startDate.AddDays(World.Day);
            return date.Day + " " + Ui.Months[date.Month - 1] + " " + date.Year;
        }

        private void ApplyWorldChanges()
        {
            if (_seenOwnersVersion != World.OwnersVersion)
            {
                _seenOwnersVersion = World.OwnersVersion;
                _ownersChanged = true;
                ApplyOwners();
            }

            RefreshDerived();
        }

        // Şehir el değiştirdi: illerin sahibi güncellenir, yeni isyancı devletin adı ve ilişkisi yazılır.
        private void ApplyOwners()
        {
            for (int city = 0; city < World.CityCount; city++)
            {
                ProvinceOwner[city] = World.CityOwner[city];
            }

            for (int country = World.BaseCountryCount; country < World.CountryCount; country++)
            {
                int parent = World.RebelParent[country];
                if (parent != WorldState.NoCountry && _countryTitles[country] == null)
                {
                    _countryTitles[country] = string.Format(Ui.RebelStateTitle, _countryTitles[parent]);
                    Relations[country] = parent == Player ? CountryRelation.War : CountryRelation.Peace;
                }
            }
        }

        private void RefreshDerived()
        {
            Array.Clear(ProvinceAlarm, 0, ProvinceAlarm.Length);
            Array.Clear(ProvinceReady, 0, ProvinceReady.Length);
            Array.Clear(ProvinceRebellion, 0, ProvinceRebellion.Length);
            Rebellions.CollectCities(World, Player, RebellionCities);
            foreach (int city in RebellionCities)
            {
                ProvinceRebellion[city] = true;
            }

            int alarms = 0;
            int ready = 0;
            int end = World.CountryFacilityStart[Player + 1];
            for (int i = World.CountryFacilityStart[Player]; i < end; i++)
            {
                int slot = World.CountrySlots[i];
                int city = World.FacilityCity[slot];
                if (World.FacilityAlarm[slot] == WorldState.AlarmActive && !ProvinceAlarm[city])
                {
                    ProvinceAlarm[city] = true;
                    alarms++;
                }

                if (!ProvinceReady[city] && FacilityCommands.IsReadyToUpgrade(Definition, World, slot))
                {
                    ProvinceReady[city] = true;
                    ready++;
                }
            }

            AlarmProvinceCount = alarms;
            ReadyProvinceCount = ready;
        }

        // Ülke adları ve yol ücretinin dayandığı başkentler arası mesafe veri dosyasından gelir.
        private void ApplyCapitals()
        {
            int count = Map.Countries.Length;
            var lat = new double[count];
            var lon = new double[count];
            for (int country = 0; country < count; country++)
            {
                _countryTitles[country] = Map.Countries[country];
                foreach (CountryViewData view in Ui.Countries)
                {
                    if (view.Key != Map.Countries[country])
                    {
                        continue;
                    }

                    _countryTitles[country] = view.Title;
                    int capital = Array.IndexOf(Map.ProvinceIds, view.Capital);
                    if (capital >= 0)
                    {
                        lat[country] = Map.CenterY[capital] / MapLayout.LatScale;
                        lon[country] = Map.CenterX[capital] / MapLayout.LonScale;
                    }
                }
            }

            CountryDistances.SetFromCapitals(World, lat, lon);

            var capitalX = new float[count];
            var capitalY = new float[count];
            for (int country = 0; country < count; country++)
            {
                capitalX[country] = (float)lon[country] * MapLayout.LonScale;
                capitalY[country] = (float)lat[country] * MapLayout.LatScale;
            }

            World.AirPathCrossings = MapRoutes.AirPathCrossings(Map, capitalX, capitalY, World.CountryCount);
        }

        // Şehir komşulukları ve kara sınırları haritadan gelir. Örnek dünyanın komşuluğu silinir, oyuncunun savaşları veri dosyasından yazılır.
        private void ApplyBordersAndWars()
        {
            MapRoutes.CityNeighbors(Map, World.CityCount, out int[] start, out int[] neighbors);
            CityOwnership.SetCityNeighbors(World, start, neighbors);
            CityOwnership.RebuildLandNeighbors(World);
            for (int country = 0; country < Relations.Length; country++)
            {
                if (Relations[country] == CountryRelation.War)
                {
                    CountryRoutes.SetWar(World, Player, country, true);
                }
            }

            World.RoutesDirty = true;
        }

        private static string[] MapTransportNames(EconomyDefinition definition, UiData ui)
        {
            var names = new string[definition.TransportCount];
            foreach (TransportViewData view in ui.Transports)
            {
                names[Array.IndexOf(definition.TransportKeys, view.Key)] = view.Name;
            }

            return names;
        }

        // Kıyı kenarı olan il kıyıdadır. Liman bu bilgiyi ister.
        private void MarkCoastalCities()
        {
            int[] edges = Map.CoastLines;
            for (int province = 0; province < Map.ProvinceCount; province++)
            {
                if (Map.ProvinceCountry[province] == MapData.NoCountry)
                {
                    continue;
                }

                int start = Map.VertexStart[province];
                int end = Map.VertexStart[province + 1];
                for (int i = 0; i < edges.Length; i += 2)
                {
                    if (edges[i] >= start && edges[i] < end)
                    {
                        World.CityCoastal[province] = true;
                        break;
                    }
                }
            }
        }

        private static BuildingViewData[] MapBuildingViews(EconomyDefinition definition, UiData ui)
        {
            var views = new BuildingViewData[definition.BuildingCount];
            foreach (BuildingViewData view in ui.Buildings)
            {
                views[definition.BuildingIndex(view.Key)] = view;
            }

            return views;
        }

        private static ResourceViewData[] MapResourceViews(EconomyDefinition definition, UiData ui)
        {
            var views = new ResourceViewData[definition.ResourceCount];
            foreach (ResourceViewData view in ui.Resources)
            {
                views[definition.ResourceIndex(view.Key)] = view;
            }

            return views;
        }

        private static int[] MapStartBar(EconomyDefinition definition, UiData ui)
        {
            var items = new int[ui.StartBar.Count];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = definition.ResourceIndex(ui.StartBar[i]);
            }

            return items;
        }

        private static int[] CountCities(MapData map)
        {
            var cities = new int[map.Countries.Length];
            for (int province = 0; province < map.ProvinceCount; province++)
            {
                byte country = map.ProvinceCountry[province];
                if (country != MapData.NoCountry)
                {
                    cities[country]++;
                }
            }

            return cities;
        }

        private static int Sum(int[] values)
        {
            int total = 0;
            foreach (int value in values)
            {
                total += value;
            }

            return total;
        }

        private static CountryRelation ParseRelation(string relation)
        {
            switch (relation)
            {
                case "own": return CountryRelation.Own;
                case "ally": return CountryRelation.Ally;
                case "war": return CountryRelation.War;
                default: return CountryRelation.Peace;
            }
        }
    }
}
