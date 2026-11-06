using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GeoWar.Game
{
    // İkon yollarını açılışta bir kez okur.
    public sealed class IconLibrary
    {
        private readonly Dictionary<string, List<PathCommand>> _icons = new Dictionary<string, List<PathCommand>>();

        public IconLibrary(UiData data)
        {
            foreach (IconData icon in data.Icons)
            {
                _icons.Add(icon.Key, SvgPathParser.Parse(icon.Path));
            }
        }

        public List<PathCommand> Get(string key)
        {
            return _icons[key];
        }
    }

    // Tasarımdaki 24 birimlik ikonları çizer. Sadece görünümü değişince yeniden çizilir.
    public sealed class IconElement : VisualElement
    {
        private const float ViewSize = 24f;

        private readonly List<PathCommand> _path;
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
