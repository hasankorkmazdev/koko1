using UnityEngine;

namespace GeoWar.Game
{
    public enum CountryRelation : byte
    {
        Own,
        Ally,
        Peace,
        War
    }

    // Haritanın renkleri ve çizgi kalınlıkları. Değerler onaylanan ekran tasarımındandır.
    public static class MapPalette
    {
        public const float OwnLineWidth = 1f;
        public const float ForeignLineWidth = 0.7f;
        public const float CountryLineWidth = 1.6f;
        public const float CoastGlowWidth = 6f;
        public const float GridWidth = 1f;
        public const float SelectedLineWidth = 2.5f;

        public static readonly Color32 Sea = new Color32(0x1F, 0x2F, 0x33, 255);
        public static readonly Color32 Land = new Color32(0x4E, 0x4A, 0x38, 255);
        public static readonly Color32 CoastGlow = new Color32(0x3F, 0x5A, 0x5C, 255);
        public static readonly Color32 Grid = new Color32(255, 255, 255, 13);
        public static readonly Color32 CountryLine = new Color32(0x0A, 0x09, 0x06, 255);
        public static readonly Color32 Selected = new Color32(0xCF, 0xAB, 0x52, 255);
        public static readonly Color32 SelectedLine = new Color32(0xF6, 0xEB, 0xC2, 255);

        private static readonly Color32[] OwnTones =
        {
            new Color32(0x4F, 0x78, 0x98, 255),
            new Color32(0x47, 0x6E, 0x8C, 255),
            new Color32(0x5A, 0x83, 0xA2, 255),
            new Color32(0x3F, 0x65, 0x81, 255)
        };

        private static readonly Color32[] PeaceTones =
        {
            new Color32(0xBD, 0xB6, 0x9C, 255),
            new Color32(0xB0, 0xA9, 0x8F, 255)
        };

        private static readonly Color32[] AllyTones =
        {
            new Color32(0x8F, 0xA8, 0x6B, 255),
            new Color32(0x82, 0x9C, 0x60, 255)
        };

        private static readonly Color32[] WarTones =
        {
            new Color32(0x8E, 0x3B, 0x2E, 255),
            new Color32(0x7E, 0x32, 0x26, 255)
        };

        private static readonly Color32 OwnLine = new Color32(0x14, 0x23, 0x2E, 255);
        private static readonly Color32 PeaceLine = new Color32(0x7B, 0x75, 0x60, 255);
        private static readonly Color32 AllyLine = new Color32(0x3E, 0x52, 0x28, 255);
        private static readonly Color32 WarLine = new Color32(0x2E, 0x0F, 0x09, 255);

        public static Color32 Fill(CountryRelation relation, int tone)
        {
            Color32[] tones = relation == CountryRelation.Own ? OwnTones
                : relation == CountryRelation.Ally ? AllyTones
                : relation == CountryRelation.War ? WarTones
                : PeaceTones;
            return tones[tone % tones.Length];
        }

        public static Color32 ProvinceLine(CountryRelation relation)
        {
            return relation == CountryRelation.Own ? OwnLine
                : relation == CountryRelation.Ally ? AllyLine
                : relation == CountryRelation.War ? WarLine
                : PeaceLine;
        }

        public static float ProvinceLineWidth(CountryRelation relation)
        {
            return relation == CountryRelation.Own ? OwnLineWidth : ForeignLineWidth;
        }
    }
}
