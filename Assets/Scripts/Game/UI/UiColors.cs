using UnityEngine;

namespace GeoWar.Game
{
    // Kodda seçilen arayüz renkleri; sabit olanlar GeoWar.uss dosyasındadır.
    public static class UiColors
    {
        public static readonly Color Text = Hex("#E8E0C8");
        public static readonly Color Good = Hex("#9DBB6A");
        public static readonly Color Warn = Hex("#E08A55");
        public static readonly Color Gold = Hex("#CFAB52");
        public static readonly Color Alarm = Hex("#D9652F");
        public static readonly Color DarkText = Hex("#15110A");

        public static readonly Color HeaderTop = Hex("#3A392A");
        public static readonly Color HeaderBottom = Hex("#201F16");
        public static readonly Color HeaderInset = Hex("#6E6C4E");

        public static readonly Color ButtonTop = Hex("#4C4B37");
        public static readonly Color ButtonBottom = Hex("#2D2C20");
        public static readonly Color ButtonInset = Hex("#77745A");
        public static readonly Color ButtonInsetBottom = Hex("#191811");

        public static readonly Color ActiveTop = Hex("#D9BA63");
        public static readonly Color ActiveBottom = Hex("#A88A3A");
        public static readonly Color ActiveInset = Hex("#F0E2A8");

        public static readonly Color Well = Hex("#14130C");
        public static readonly Color SelectedBorder = Hex("#CFAB52");

        public static readonly Color LegendOwn = Hex("#4F7898");
        public static readonly Color LegendAlly = Hex("#8FA86B");
        public static readonly Color LegendPeace = Hex("#BDB69C");
        public static readonly Color LegendWar = Hex("#8E3B2E");

        public static readonly Color RivetCore = Hex("#A39C72");
        public static readonly Color RivetRing = Hex("#15140D");
        public static readonly Color RivetBorder = new Color(207f / 255f, 171f / 255f, 82f / 255f, 0.25f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.45f);
        public static readonly Color Vignette = new Color(0f, 0f, 0f, 0.3f);
        public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        public static Color Hex(string value)
        {
            Color color;
            ColorUtility.TryParseHtmlString(value, out color);
            return color;
        }
    }
}
