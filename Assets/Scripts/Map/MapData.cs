using System.IO;

namespace GeoWar.Map
{
    // Çizime hazır harita. İller önceden üçgenlenmiş olarak dosyadan yüklenir, telefonda hesaplanmaz.
    // Koordinatlar harita birimindedir: x doğuya, y kuzeye doğru artar.
    public sealed class MapData
    {
        private const int FormatVersion = 2;
        public const byte NoCountry = byte.MaxValue;

        public string[] Countries;

        public int ProvinceCount;
        public string[] ProvinceIds;
        public string[] ProvinceNames;
        public byte[] ProvinceCountry;
        public byte[] ProvinceTone;
        public float[] CenterX;
        public float[] CenterY;
        public float[] MinX;
        public float[] MinY;
        public float[] MaxX;
        public float[] MaxY;

        // Üçgenler: bir ilin köşeleri VertexStart aralığında, üçgenleri TriangleStart aralığındadır.
        public int[] VertexStart;
        public float[] VertexX;
        public float[] VertexY;
        public int[] TriangleStart;
        public int[] Triangles;

        // Dokunulan ili bulmak için halkalar: bir ilin halkaları RingStart aralığındadır.
        public int[] ProvinceRingStart;
        public int[] RingStart;

        // Çizgiler: iki köşe indeksi bir kenardır, köşeler VertexX/VertexY içindedir.
        public int[] ProvinceBorderLines;
        public int[] CountryBorderLines;

        // Tüm karaların denizle kesiştiği kenarlar; kıyı parıltısı bunlardan çizilir.
        public int[] CoastLines;

        public void WriteTo(Stream stream)
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(FormatVersion);
                WriteStrings(writer, Countries);
                writer.Write(ProvinceCount);
                WriteStrings(writer, ProvinceIds);
                WriteStrings(writer, ProvinceNames);
                WriteBytes(writer, ProvinceCountry);
                WriteBytes(writer, ProvinceTone);
                WriteFloats(writer, CenterX);
                WriteFloats(writer, CenterY);
                WriteFloats(writer, MinX);
                WriteFloats(writer, MinY);
                WriteFloats(writer, MaxX);
                WriteFloats(writer, MaxY);
                WriteInts(writer, VertexStart);
                WriteFloats(writer, VertexX);
                WriteFloats(writer, VertexY);
                WriteInts(writer, TriangleStart);
                WriteInts(writer, Triangles);
                WriteInts(writer, ProvinceRingStart);
                WriteInts(writer, RingStart);
                WriteInts(writer, ProvinceBorderLines);
                WriteInts(writer, CountryBorderLines);
                WriteInts(writer, CoastLines);
            }
        }

        public static MapData ReadFrom(Stream stream)
        {
            using (var reader = new BinaryReader(stream))
            {
                int version = reader.ReadInt32();
                if (version != FormatVersion)
                {
                    throw new InvalidDataException("Harita dosyası sürümü desteklenmiyor: " + version);
                }

                var map = new MapData();
                map.Countries = ReadStrings(reader);
                map.ProvinceCount = reader.ReadInt32();
                map.ProvinceIds = ReadStrings(reader);
                map.ProvinceNames = ReadStrings(reader);
                map.ProvinceCountry = ReadBytes(reader);
                map.ProvinceTone = ReadBytes(reader);
                map.CenterX = ReadFloats(reader);
                map.CenterY = ReadFloats(reader);
                map.MinX = ReadFloats(reader);
                map.MinY = ReadFloats(reader);
                map.MaxX = ReadFloats(reader);
                map.MaxY = ReadFloats(reader);
                map.VertexStart = ReadInts(reader);
                map.VertexX = ReadFloats(reader);
                map.VertexY = ReadFloats(reader);
                map.TriangleStart = ReadInts(reader);
                map.Triangles = ReadInts(reader);
                map.ProvinceRingStart = ReadInts(reader);
                map.RingStart = ReadInts(reader);
                map.ProvinceBorderLines = ReadInts(reader);
                map.CountryBorderLines = ReadInts(reader);
                map.CoastLines = ReadInts(reader);
                return map;
            }
        }

        private static void WriteStrings(BinaryWriter writer, string[] values)
        {
            writer.Write(values.Length);
            foreach (string value in values)
            {
                writer.Write(value);
            }
        }

        private static string[] ReadStrings(BinaryReader reader)
        {
            var values = new string[reader.ReadInt32()];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = reader.ReadString();
            }

            return values;
        }

        private static void WriteBytes(BinaryWriter writer, byte[] values)
        {
            writer.Write(values.Length);
            writer.Write(values);
        }

        private static byte[] ReadBytes(BinaryReader reader)
        {
            return reader.ReadBytes(reader.ReadInt32());
        }

        private static void WriteInts(BinaryWriter writer, int[] values)
        {
            writer.Write(values.Length);
            foreach (int value in values)
            {
                writer.Write(value);
            }
        }

        private static int[] ReadInts(BinaryReader reader)
        {
            var values = new int[reader.ReadInt32()];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = reader.ReadInt32();
            }

            return values;
        }

        private static void WriteFloats(BinaryWriter writer, float[] values)
        {
            writer.Write(values.Length);
            foreach (float value in values)
            {
                writer.Write(value);
            }
        }

        private static float[] ReadFloats(BinaryReader reader)
        {
            var values = new float[reader.ReadInt32()];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = reader.ReadSingle();
            }

            return values;
        }
    }
}
