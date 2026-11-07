using System;
using System.Collections.Generic;

namespace GeoWar.Map
{
    // Kıyı adayı kenarın dışına bakar: dışarıda oyunda olmayan bir ülkenin karası varsa kenar kıyı değil, sınırdır.
    // Arka plan karası sadeleştirildiği için illerle birebir kenar paylaşmaz; eşleşmeyen her kenar deniz sayılamaz.
    public sealed class LandProbe
    {
        private const float CellSize = 30f;
        private const float Half = 0.5f;
        private const int ProbeSteps = 4;

        private readonly float[] _x;
        private readonly float[] _y;
        private readonly int[] _ringStart;
        private readonly bool[] _isBackground;
        private readonly bool[] _isCounterClockwise;
        private readonly float[] _minX;
        private readonly float[] _minY;
        private readonly float[] _maxX;
        private readonly float[] _maxY;
        private readonly Dictionary<(int, int), List<int>> _cells = new Dictionary<(int, int), List<int>>();

        // ringStart halka sayısından bir uzundur; son değeri toplam köşe sayısıdır.
        public LandProbe(float[] x, float[] y, int[] ringStart, bool[] isBackground)
        {
            _x = x;
            _y = y;
            _ringStart = ringStart;
            _isBackground = isBackground;

            int ringCount = ringStart.Length - 1;
            _isCounterClockwise = new bool[ringCount];
            _minX = new float[ringCount];
            _minY = new float[ringCount];
            _maxX = new float[ringCount];
            _maxY = new float[ringCount];
            for (int ring = 0; ring < ringCount; ring++)
            {
                MeasureRing(ring);
                AddToCells(ring);
            }
        }

        // Seçilebilir iller ortak kenarlarını birebir paylaşır; onların karşı kıyısı boğaz da olsa denizdir.
        public bool HasLandBeyond(int a, int b, int ring, float distance)
        {
            float dx = _x[b] - _x[a];
            float dy = _y[b] - _y[a];
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length <= 0f)
            {
                return false;
            }

            // Saat yönünün tersine giden halkada iç sol taraftır, dışarısı sağ taraftır.
            float side = _isCounterClockwise[ring] ? 1f : -1f;
            float middleX = (_x[a] + _x[b]) * Half;
            float middleY = (_y[a] + _y[b]) * Half;

            // Tek noktaya bakmak yetmez: ince bir kara şeridinin ötesi yine aynı ülke olabilir.
            for (int step = 1; step <= ProbeSteps; step++)
            {
                float reach = distance * step / ProbeSteps;
                if (IsOtherLand(middleX + side * dy / length * reach, middleY - side * dx / length * reach, ring))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsOtherLand(float x, float y, int ring)
        {
            if (!_cells.TryGetValue(Cell(x, y), out List<int> rings))
            {
                return false;
            }

            foreach (int other in rings)
            {
                bool isCandidate = other != ring && (_isBackground[other] || _isBackground[ring]);
                if (isCandidate && Contains(other, x, y))
                {
                    return true;
                }
            }

            return false;
        }

        private bool Contains(int ring, float x, float y)
        {
            if (x < _minX[ring] || x > _maxX[ring] || y < _minY[ring] || y > _maxY[ring])
            {
                return false;
            }

            bool inside = false;
            int start = _ringStart[ring];
            int end = _ringStart[ring + 1];
            for (int i = start, j = end - 1; i < end; j = i, i++)
            {
                if ((_y[i] > y) != (_y[j] > y) && x < (_x[j] - _x[i]) * (y - _y[i]) / (_y[j] - _y[i]) + _x[i])
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private void MeasureRing(int ring)
        {
            int start = _ringStart[ring];
            int end = _ringStart[ring + 1];
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            double sum = 0;
            for (int i = start, j = end - 1; i < end; j = i, i++)
            {
                sum += (double)_x[j] * _y[i] - (double)_x[i] * _y[j];
                minX = Math.Min(minX, _x[i]);
                minY = Math.Min(minY, _y[i]);
                maxX = Math.Max(maxX, _x[i]);
                maxY = Math.Max(maxY, _y[i]);
            }

            _isCounterClockwise[ring] = sum > 0;
            _minX[ring] = minX;
            _minY[ring] = minY;
            _maxX[ring] = maxX;
            _maxY[ring] = maxY;
        }

        private void AddToCells(int ring)
        {
            (int firstX, int firstY) = Cell(_minX[ring], _minY[ring]);
            (int lastX, int lastY) = Cell(_maxX[ring], _maxY[ring]);
            for (int cellY = firstY; cellY <= lastY; cellY++)
            {
                for (int cellX = firstX; cellX <= lastX; cellX++)
                {
                    if (!_cells.TryGetValue((cellX, cellY), out List<int> rings))
                    {
                        rings = new List<int>(2);
                        _cells.Add((cellX, cellY), rings);
                    }

                    rings.Add(ring);
                }
            }
        }

        private static (int, int) Cell(float x, float y)
        {
            return ((int)Math.Floor(x / CellSize), (int)Math.Floor(y / CellSize));
        }
    }
}
