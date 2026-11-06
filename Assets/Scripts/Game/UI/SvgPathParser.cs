using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeoWar.Game
{
    public enum PathOp : byte
    {
        Move,
        Line,
        Cubic,
        Close
    }

    public struct PathCommand
    {
        public PathOp Op;
        public float X1;
        public float Y1;
        public float X2;
        public float Y2;
        public float X;
        public float Y;
    }

    // Tasarımdaki ikonların SVG yollarını çizime hazır komutlara çevirir.
    // Yay komutları eğriye çevrilir; sonuç açılışta bir kez üretilip saklanır.
    public static class SvgPathParser
    {
        private const double MaxArcStep = Math.PI / 2;
        private const double RightAngleRatio = 4.0 / 3.0;

        public static List<PathCommand> Parse(string path)
        {
            var commands = new List<PathCommand>();
            int index = 0;
            char command = ' ';
            float x = 0;
            float y = 0;
            float startX = 0;
            float startY = 0;
            float controlX = 0;
            float controlY = 0;
            bool hasControl = false;

            while (true)
            {
                SkipSeparators(path, ref index);
                if (index >= path.Length)
                {
                    return commands;
                }

                if (char.IsLetter(path[index]))
                {
                    command = path[index];
                    index++;
                }
                else if (command == 'M')
                {
                    // Taşıma komutundan sonraki sayılar çizgi sayılır.
                    command = 'L';
                }
                else if (command == 'm')
                {
                    command = 'l';
                }

                bool relative = char.IsLower(command);
                float baseX = relative ? x : 0;
                float baseY = relative ? y : 0;
                bool keepControl = false;

                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                        x = baseX + ReadNumber(path, ref index);
                        y = baseY + ReadNumber(path, ref index);
                        startX = x;
                        startY = y;
                        commands.Add(new PathCommand { Op = PathOp.Move, X = x, Y = y });
                        break;
                    case 'L':
                        x = baseX + ReadNumber(path, ref index);
                        y = baseY + ReadNumber(path, ref index);
                        commands.Add(new PathCommand { Op = PathOp.Line, X = x, Y = y });
                        break;
                    case 'H':
                        x = baseX + ReadNumber(path, ref index);
                        commands.Add(new PathCommand { Op = PathOp.Line, X = x, Y = y });
                        break;
                    case 'V':
                        y = baseY + ReadNumber(path, ref index);
                        commands.Add(new PathCommand { Op = PathOp.Line, X = x, Y = y });
                        break;
                    case 'C':
                    {
                        float x1 = baseX + ReadNumber(path, ref index);
                        float y1 = baseY + ReadNumber(path, ref index);
                        controlX = baseX + ReadNumber(path, ref index);
                        controlY = baseY + ReadNumber(path, ref index);
                        x = baseX + ReadNumber(path, ref index);
                        y = baseY + ReadNumber(path, ref index);
                        commands.Add(new PathCommand { Op = PathOp.Cubic, X1 = x1, Y1 = y1, X2 = controlX, Y2 = controlY, X = x, Y = y });
                        keepControl = true;
                        break;
                    }
                    case 'S':
                    {
                        // İlk kontrol noktası önceki eğrinin yansımasıdır.
                        float x1 = hasControl ? 2 * x - controlX : x;
                        float y1 = hasControl ? 2 * y - controlY : y;
                        controlX = baseX + ReadNumber(path, ref index);
                        controlY = baseY + ReadNumber(path, ref index);
                        x = baseX + ReadNumber(path, ref index);
                        y = baseY + ReadNumber(path, ref index);
                        commands.Add(new PathCommand { Op = PathOp.Cubic, X1 = x1, Y1 = y1, X2 = controlX, Y2 = controlY, X = x, Y = y });
                        keepControl = true;
                        break;
                    }
                    case 'A':
                    {
                        float radiusX = ReadNumber(path, ref index);
                        float radiusY = ReadNumber(path, ref index);
                        float rotation = ReadNumber(path, ref index);
                        bool largeArc = ReadFlag(path, ref index);
                        bool sweep = ReadFlag(path, ref index);
                        float endX = baseX + ReadNumber(path, ref index);
                        float endY = baseY + ReadNumber(path, ref index);
                        AddArc(commands, x, y, radiusX, radiusY, rotation, largeArc, sweep, endX, endY);
                        x = endX;
                        y = endY;
                        break;
                    }
                    case 'Z':
                        x = startX;
                        y = startY;
                        commands.Add(new PathCommand { Op = PathOp.Close, X = x, Y = y });
                        break;
                    default:
                        throw new FormatException("Desteklenmeyen yol komutu: " + command);
                }

                hasControl = keepControl;
            }
        }

        private static void AddArc(
            List<PathCommand> commands,
            double fromX,
            double fromY,
            double radiusX,
            double radiusY,
            double rotationDegrees,
            bool largeArc,
            bool sweep,
            double toX,
            double toY)
        {
            if (radiusX == 0 || radiusY == 0 || (fromX == toX && fromY == toY))
            {
                commands.Add(new PathCommand { Op = PathOp.Line, X = (float)toX, Y = (float)toY });
                return;
            }

            radiusX = Math.Abs(radiusX);
            radiusY = Math.Abs(radiusY);
            double rotation = rotationDegrees * Math.PI / 180;
            double cos = Math.Cos(rotation);
            double sin = Math.Sin(rotation);

            // SVG belirtimindeki uç nokta biçiminden merkez biçimine çeviri.
            double midX = (fromX - toX) / 2;
            double midY = (fromY - toY) / 2;
            double localX = cos * midX + sin * midY;
            double localY = -sin * midX + cos * midY;

            double scale = localX * localX / (radiusX * radiusX) + localY * localY / (radiusY * radiusY);
            if (scale > 1)
            {
                radiusX *= Math.Sqrt(scale);
                radiusY *= Math.Sqrt(scale);
            }

            double top = radiusX * radiusX * radiusY * radiusY - radiusX * radiusX * localY * localY - radiusY * radiusY * localX * localX;
            double bottom = radiusX * radiusX * localY * localY + radiusY * radiusY * localX * localX;
            double factor = Math.Sqrt(Math.Max(0, top / bottom)) * (largeArc == sweep ? -1 : 1);
            double centerLocalX = factor * radiusX * localY / radiusY;
            double centerLocalY = -factor * radiusY * localX / radiusX;
            double centerX = cos * centerLocalX - sin * centerLocalY + (fromX + toX) / 2;
            double centerY = sin * centerLocalX + cos * centerLocalY + (fromY + toY) / 2;

            double startAngle = Math.Atan2((localY - centerLocalY) / radiusY, (localX - centerLocalX) / radiusX);
            double endAngle = Math.Atan2((-localY - centerLocalY) / radiusY, (-localX - centerLocalX) / radiusX);
            double delta = endAngle - startAngle;
            if (sweep && delta < 0)
            {
                delta += 2 * Math.PI;
            }
            else if (!sweep && delta > 0)
            {
                delta -= 2 * Math.PI;
            }

            int steps = (int)Math.Ceiling(Math.Abs(delta) / MaxArcStep - 1e-9);
            double step = delta / steps;
            double handle = RightAngleRatio * Math.Tan(step / 4);
            double angle = startAngle;

            for (int i = 0; i < steps; i++)
            {
                double next = angle + step;
                double cosA = Math.Cos(angle);
                double sinA = Math.Sin(angle);
                double cosB = Math.Cos(next);
                double sinB = Math.Sin(next);

                double x1 = cosA - handle * sinA;
                double y1 = sinA + handle * cosA;
                double x2 = cosB + handle * sinB;
                double y2 = sinB - handle * cosB;

                // Son nokta yuvarlama hatası birikmesin diye verilen uç noktadır.
                bool last = i == steps - 1;
                commands.Add(new PathCommand
                {
                    Op = PathOp.Cubic,
                    X1 = (float)(centerX + cos * radiusX * x1 - sin * radiusY * y1),
                    Y1 = (float)(centerY + sin * radiusX * x1 + cos * radiusY * y1),
                    X2 = (float)(centerX + cos * radiusX * x2 - sin * radiusY * y2),
                    Y2 = (float)(centerY + sin * radiusX * x2 + cos * radiusY * y2),
                    X = (float)(last ? toX : centerX + cos * radiusX * cosB - sin * radiusY * sinB),
                    Y = (float)(last ? toY : centerY + sin * radiusX * cosB + cos * radiusY * sinB)
                });

                angle = next;
            }
        }

        private static void SkipSeparators(string path, ref int index)
        {
            while (index < path.Length && (path[index] == ' ' || path[index] == ',' || path[index] == '\n' || path[index] == '\t'))
            {
                index++;
            }
        }

        private static bool ReadFlag(string path, ref int index)
        {
            SkipSeparators(path, ref index);
            bool flag = path[index] == '1';
            index++;
            return flag;
        }

        private static float ReadNumber(string path, ref int index)
        {
            SkipSeparators(path, ref index);
            int start = index;
            if (index < path.Length && (path[index] == '-' || path[index] == '+'))
            {
                index++;
            }

            bool seenDot = false;
            while (index < path.Length)
            {
                char c = path[index];
                if (char.IsDigit(c))
                {
                    index++;
                }
                else if (c == '.' && !seenDot)
                {
                    seenDot = true;
                    index++;
                }
                else
                {
                    break;
                }
            }

            if (index == start)
            {
                throw new FormatException("Yolda sayı bekleniyordu, konum " + start + ": " + path);
            }

            return float.Parse(path.Substring(start, index - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
