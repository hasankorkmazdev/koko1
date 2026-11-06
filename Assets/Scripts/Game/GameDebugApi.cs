#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using System.Text;
using GeoWar.Map;
using GeoWar.Simulation;
using UnityEngine;

namespace GeoWar.Game
{
    // Geliştirme aracının oyunla konuştuğu kapı: durumu okur, ekrana dokunur. Oyuncuya giden sürüme girmez.
    // Koordinatlar tasarım birimindedir: sol üst köşe 0,0.
    public static class GameDebugApi
    {
        // Oyun döngüsü her karede çağırır: araç, editör yavaşlasa da komutlarını işleyebilsin.
        public static System.Action Pump;

        public static bool IsRunning
        {
            get { return GameRunner.Current != null; }
        }

        public static string State()
        {
            GameRunner runner = GameRunner.Current;
            GameSession session = runner.Session;
            GameUi ui = runner.Ui;
            MapCameraController cameraController = runner.CameraController;
            int selected = ui.SelectedProvince;

            var json = new JsonText();
            json.Add("day", session.World.Day);
            json.Add("date", session.DateText());
            json.Add("speed", session.Speed);
            json.Add("dayProgress", session.DayProgress);
            json.Add("money", session.PlayerMoney());
            json.Add("satisfaction", session.PlayerSatisfactionPercent());
            json.Add("energy", session.PlayerEnergyPercent());
            json.Add("alarmProvinces", session.AlarmProvinceCount);
            json.Add("readyProvinces", session.ReadyProvinceCount);
            json.Add("builtFacilities", session.World.CountBuiltFacilities());
            json.Add("selected", selected == MapHitTest.NotFound ? "" : session.Map.ProvinceNames[selected]);
            json.Add("builderOpen", ui.IsBuilderOpen);
            json.Add("resourceCell", ui.OpenResourceCell);
            json.Add("camX", cameraController.Center.x);
            json.Add("camY", cameraController.Center.y);
            json.Add("zoom", cameraController.Zoom);
            json.Add("screenWidth", Screen.width);
            json.Add("screenHeight", Screen.height);
            json.Add("viewWidth", MapLayout.ViewWidth);
            json.Add("viewHeight", MapLayout.ViewHeight);
            json.Add("fps", Time.smoothDeltaTime > 0f ? 1f / Time.smoothDeltaTime : 0f);
            json.Add("secondsSinceInput", Time.realtimeSinceStartup - cameraController.LastInputTime);
            return json.Close();
        }

        // Verilen noktada ekranda ne varsa ona basar; sonucu ne olduğunu söyler.
        public static string Tap(float x, float y)
        {
            return GameRunner.Current.Ui.DebugTap(x, y);
        }

        // Görünen yazılardan içinde bu metin geçenin ortasına basar.
        public static string TapText(string text)
        {
            return GameRunner.Current.Ui.DebugTapText(text);
        }

        public static void Pan(float dx, float dy)
        {
            GameRunner.Current.CameraController.DebugPan(dx, dy);
        }

        public static void Zoom(float factor)
        {
            GameRunner.Current.CameraController.DebugZoom(factor);
        }

        public static void Speed(int speed)
        {
            GameRunner.Current.Session.SetSpeed(speed);
        }

        // İli adıyla seçer; dokunma denemesi için koordinat aramak gerekmez.
        public static bool SelectProvince(string name)
        {
            GameRunner runner = GameRunner.Current;
            int province = System.Array.IndexOf(runner.Session.Map.ProvinceNames, name);
            if (province < 0)
            {
                return false;
            }

            runner.Ui.SelectProvince(province);
            return true;
        }

        // Görünen tüm yazılar, konumları ve kutusuna sığıp sığmadıkları.
        public static string Labels(bool overflowOnly)
        {
            return GameRunner.Current.Ui.DebugLabels(overflowOnly);
        }
    }

    // Küçük JSON yazıcı; sadece geliştirme aracı kullanır.
    public sealed class JsonText
    {
        private readonly StringBuilder _text = new StringBuilder("{");
        private bool _hasField;

        public void Add(string key, string value)
        {
            Key(key);
            _text.Append(Quote(value));
        }

        public void Add(string key, long value)
        {
            Key(key);
            _text.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public void Add(string key, float value)
        {
            Key(key);
            _text.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        public void Add(string key, bool value)
        {
            Key(key);
            _text.Append(value ? "true" : "false");
        }

        // Hazır JSON parçasını olduğu gibi ekler.
        public void AddRaw(string key, string rawJson)
        {
            Key(key);
            _text.Append(rawJson);
        }

        public string Close()
        {
            return _text.Append('}').ToString();
        }

        public static string Quote(string value)
        {
            var text = new StringBuilder("\"");
            foreach (char c in value ?? "")
            {
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            text.Append(' ');
                        }
                        else
                        {
                            text.Append(c);
                        }

                        break;
                }
            }

            return text.Append('"').ToString();
        }

        private void Key(string key)
        {
            if (_hasField)
            {
                _text.Append(',');
            }

            _hasField = true;
            _text.Append(Quote(key)).Append(':');
        }
    }
}
#endif
