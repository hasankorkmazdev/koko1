using UnityEngine;

namespace GeoWar.Game
{
    // Ekran tasarımı 390 x 844 birime göre yapıldı; arayüz ve harita aynı ölçeği paylaşır.
    // Ölçek tasarımın ekrana sığacağı kadardır: uzun telefonda genişlik, tablet ve yatay pencerede yükseklik belirler.
    // Zoom, bir harita biriminin kaç tasarım birimi ettiğidir.
    public static class MapLayout
    {
        public const float DesignWidth = 390f;
        public const float DesignHeight = 844f;
        public const float LonScale = 30f;
        public const float LatScale = 38.6f;
        public const float MinZoom = 0.5f;
        public const float MaxZoom = 5f;
        public const float StartZoom = 0.5f;
        public const float CountryCenterLon = 34.8f;
        public const float CountryCenterLat = 39f;

        // Boş ekranda ülke merkezinin duracağı tasarım yüksekliği.
        public const float OpenFocusY = 440f;

        // Haritanın kaydırılabildiği alan, harita biriminde: boylam 15-192 (Çukotka 191'e uzanır), enlem 14-84.
        public static readonly Rect PanBounds = Rect.MinMaxRect(450f, 540.4f, 5760f, 3242.4f);

        public static float UiScale
        {
            get { return Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight); }
        }

        // Ekranın tasarım birimi cinsinden boyutu; en az tasarım kadardır.
        public static float ViewWidth
        {
            get { return Screen.width / UiScale; }
        }

        public static float ViewHeight
        {
            get { return Screen.height / UiScale; }
        }

        // Arayüz panelinin ölçek kuralı: 0 genişliğe, 1 yüksekliğe uyar.
        public static float PanelMatch
        {
            get { return Screen.width / DesignWidth <= Screen.height / DesignHeight ? 0f : 1f; }
        }

        public static Vector2 ToWorld(float lon, float lat)
        {
            return new Vector2(lon * LonScale, lat * LatScale);
        }
    }
}
