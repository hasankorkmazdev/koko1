using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // İkon yollarını ve kaynak görsellerini açılışta bir kez okur.
    public sealed class IconLibrary
    {
        private readonly Dictionary<string, List<PathCommand>> _icons = new Dictionary<string, List<PathCommand>>();
        private readonly Dictionary<string, Texture2D> _resourceImages = new Dictionary<string, Texture2D>();
        private readonly Texture2D _treasuryImage;
        private readonly Texture2D _moneyImage;
        private readonly Texture2D _populationImage;
        private readonly Texture2D _satisfactionHighImage;
        private readonly Texture2D _satisfactionLowImage;

        public IconLibrary(UiData data)
        {
            _treasuryImage = Resources.Load<Texture2D>(data.TreasuryIcon);
            _moneyImage = Resources.Load<Texture2D>(data.MoneyIcon);
            _populationImage = Resources.Load<Texture2D>(data.PopulationIcon);
            _satisfactionHighImage = Resources.Load<Texture2D>(data.SatisfactionHighIcon);
            _satisfactionLowImage = Resources.Load<Texture2D>(data.SatisfactionLowIcon);

            foreach (IconData icon in data.Icons)
            {
                _icons.Add(icon.Key, SvgPathParser.Parse(icon.Path));
            }

            foreach (ResourceGroupData group in data.ResourceGroups)
            {
                foreach (string key in group.Keys)
                {
                    Texture2D image = Resources.Load<Texture2D>(group.IconFolder + "/" + key);
                    if (image != null)
                    {
                        _resourceImages.Add(key, image);
                    }
                }
            }
        }

        public List<PathCommand> Get(string key)
        {
            return _icons[key];
        }

        // Görseli henüz üretilmemiş kaynak, ui.json'daki vektör ikonuyla çizilir.
        public VisualElement CreateResourceIcon(ResourceViewData view, float strokeWidth)
        {
            Texture2D image;
            if (!_resourceImages.TryGetValue(view.Key, out image))
            {
                return new IconElement(Get(view.Icon), UiColors.Hex(view.Tint), strokeWidth);
            }

            return CreateImage(image);
        }

        public VisualElement CreateTreasuryIcon()
        {
            return CreateImage(_treasuryImage);
        }

        public VisualElement CreateMoneyIcon()
        {
            return CreateImage(_moneyImage);
        }

        public VisualElement CreatePopulationIcon()
        {
            return CreateImage(_populationImage);
        }

        public VisualElement CreateSatisfactionIcon()
        {
            return CreateImage(_satisfactionHighImage);
        }

        // Memnuniyet eşiği geçince aynı öğenin görseli değişir.
        public void ShowSatisfaction(VisualElement icon, bool isLow)
        {
            icon.style.backgroundImage = new StyleBackground(isLow ? _satisfactionLowImage : _satisfactionHighImage);
        }

        private static VisualElement CreateImage(Texture2D image)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.style.backgroundImage = new StyleBackground(image);
            return element;
        }
    }

    // Tasarımdaki 24 birimlik ikonları çizer. Sadece görünümü değişince yeniden çizilir.
    public sealed class IconElement : VisualElement
    {
        private const float ViewSize = 24f;

        private List<PathCommand> _path;
        private readonly float _strokeWidth;
        private bool _filled;
        private Color _color;

        public IconElement(List<PathCommand> path, Color color, float strokeWidth, bool filled = false)
        {
            _path = path;
            _color = color;
            _strokeWidth = strokeWidth;
            _filled = filled;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetPath(List<PathCommand> path)
        {
            if (_path != path)
            {
                _path = path;
                MarkDirtyRepaint();
            }
        }

        public void SetColor(Color color)
        {
            if (_color != color)
            {
                _color = color;
                MarkDirtyRepaint();
            }
        }

        public void SetFilled(bool filled)
        {
            if (_filled != filled)
            {
                _filled = filled;
                MarkDirtyRepaint();
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            float scale = layout.width / ViewSize;
            if (float.IsNaN(scale) || scale <= 0f)
            {
                return;
            }

            Painter2D painter = context.painter2D;
            PathPainter.Trace(painter, _path, scale);
            if (_filled)
            {
                painter.fillColor = _color;
                painter.Fill(FillRule.OddEven);
            }

            if (_strokeWidth <= 0f)
            {
                return;
            }

            painter.strokeColor = _color;
            painter.lineWidth = _strokeWidth * scale;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            painter.Stroke();
        }
    }

    // Bayraklar renkli katmanlardan oluşur; çevresine tasarımdaki koyu çerçeve çizilir.
    public sealed class FlagElement : VisualElement
    {
        private const float FrameThickness = 2f;

        private readonly List<List<PathCommand>> _layers = new List<List<PathCommand>>();
        private readonly List<Color> _colors = new List<Color>();
        private FlagData _data;
        private float _viewWidth;

        public FlagElement(FlagData data)
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            SetFlag(data);
        }

        // Panel başka ülkeye geçince bayrak değişir. Bayrağı olmayan ülkede yalnızca çerçeve kalmaz, hiçbir şey çizilmez.
        public void SetFlag(FlagData data)
        {
            if (_data == data)
            {
                return;
            }

            _data = data;
            _layers.Clear();
            _colors.Clear();
            _viewWidth = data != null ? data.Width : 0f;
            if (data != null)
            {
                foreach (FlagLayerData layer in data.Layers)
                {
                    _layers.Add(SvgPathParser.Parse(layer.Path));
                    _colors.Add(UiColors.Hex(layer.Fill));
                }
            }

            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            float width = layout.width;
            float height = layout.height;
            if (_viewWidth <= 0f || float.IsNaN(width) || width <= 0f)
            {
                return;
            }

            float scale = width / _viewWidth;

            MetalPanel.AddQuad(
                context,
                -FrameThickness,
                -FrameThickness,
                width + FrameThickness * 2f,
                height + FrameThickness * 2f,
                MapPalette.CountryLine,
                MapPalette.CountryLine);

            Painter2D painter = context.painter2D;
            for (int i = 0; i < _layers.Count; i++)
            {
                PathPainter.Trace(painter, _layers[i], scale);
                painter.fillColor = _colors[i];
                painter.Fill();
            }
        }
    }

    public static class PathPainter
    {
        public static void Trace(Painter2D painter, List<PathCommand> path, float scale)
        {
            painter.BeginPath();
            for (int i = 0; i < path.Count; i++)
            {
                PathCommand command = path[i];
                switch (command.Op)
                {
                    case PathOp.Move:
                        painter.MoveTo(new Vector2(command.X * scale, command.Y * scale));
                        break;
                    case PathOp.Line:
                        painter.LineTo(new Vector2(command.X * scale, command.Y * scale));
                        break;
                    case PathOp.Cubic:
                        painter.BezierCurveTo(
                            new Vector2(command.X1 * scale, command.Y1 * scale),
                            new Vector2(command.X2 * scale, command.Y2 * scale),
                            new Vector2(command.X * scale, command.Y * scale));
                        break;
                    case PathOp.Close:
                        painter.ClosePath();
                        break;
                }
            }
        }
    }
}
