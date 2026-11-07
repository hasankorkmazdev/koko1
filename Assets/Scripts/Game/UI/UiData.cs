using System;
using System.Collections.Generic;

namespace GeoWar.Game
{
    // Assets/Data/Resources/ui.json dosyasının karşılığı: ikonlar, kaynak adları, ülkeler ve başlangıç görünümü.
    [Serializable]
    public sealed class UiData
    {
        public string PlayerCountry;
        public int StartYear;
        public int StartMonth;
        public int StartDay;
        public List<string> Months;
        public string TreasuryIcon;
        public string MoneyIcon;
        public string PopulationIcon;
        public string SatisfactionHighIcon;
        public string SatisfactionLowIcon;
        public List<IconData> Icons;
        public List<ResourceGroupData> ResourceGroups;
        public List<TabViewData> Tabs;
        public List<string> BuildMaterials;
        public List<int> MarketAmounts;
        public List<TransportViewData> Transports;
        public List<BuildingViewData> Buildings;
        public List<ResourceViewData> Resources;
        public List<string> StartBar;
        public List<CountryViewData> Countries;
        public List<SeaLabelData> SeaLabels;
        public List<FlagData> Flags;

        // İsyancı devletin adı ana ülkenin adıyla kurulur; bayrağı tektir.
        public string RebelStateTitle;
        public string RebelFlag;
    }

    [Serializable]
    public sealed class IconData
    {
        public string Key;
        public string Path;
    }

    [Serializable]
    public sealed class ResourceGroupData
    {
        public string Name;
        public string IconFolder;
        public List<string> Keys;
    }

    [Serializable]
    public sealed class TransportViewData
    {
        public string Key;
        public string Name;
    }

    [Serializable]
    public sealed class TabViewData
    {
        public string Key;
        public string Label;
        public string Note;
    }

    // Tesis türünün görünümü. Üretir yazısı boşsa reçetelerden çıkarılır.
    // ProducesIcon, ürünü kaynak olmayan tesisin (enerji, sevkiyat) yazısının yanında durur.
    [Serializable]
    public sealed class BuildingViewData
    {
        public string Key;
        public string Name;
        public string Icon;
        public string Deposit;
        public string Produces;
        public string ProducesIcon;
        public string Note;
    }

    [Serializable]
    public sealed class ResourceViewData
    {
        public string Key;
        public string Name;
        public string Icon;
        public string Tint;
        public int LowWarning;
    }

    [Serializable]
    public sealed class CountryViewData
    {
        public string Key;
        public string Name;
        public string Title;
        public float Lon;
        public float Lat;
        public string Relation;
        public string Capital;
        public int Score;
    }

    [Serializable]
    public sealed class SeaLabelData
    {
        public string Name;
        public float Lon;
        public float Lat;
        public int FontSize;
        public float LetterSpacing;
    }

    [Serializable]
    public sealed class FlagData
    {
        public string Key;
        public float Width;
        public float Height;
        public List<FlagLayerData> Layers;
    }

    [Serializable]
    public sealed class FlagLayerData
    {
        public string Path;
        public string Fill;
    }
}
