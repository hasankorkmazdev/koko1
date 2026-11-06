using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeoWar.Game
{
    // Kenarları GeoWar/ScreenLine gölgelendiricisinin açacağı şeritlere çevirir.
    public sealed class LineMeshBuilder
    {
        private const int VerticesPerSegment = 4;
        private const float PositiveSide = 1f;
        private const float NegativeSide = -1f;

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<Vector4> _sides = new List<Vector4>();
        private readonly List<int> _triangles = new List<int>();

        public int VertexCount
        {
            get { return _vertices.Count; }
        }

        public int TriangleCount
        {
            get { return _triangles.Count / 3; }
        }

        public void Add(float ax, float ay, float bx, float by, Color32 color, float width)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length <= 0f)
            {
                return;
            }

            float nx = -dy / length;
            float ny = dx / length;
            int first = _vertices.Count;

            AddVertex(ax, ay, nx, ny, width, PositiveSide, color);
            AddVertex(ax, ay, -nx, -ny, width, NegativeSide, color);
            AddVertex(bx, by, -nx, -ny, width, NegativeSide, color);
            AddVertex(bx, by, nx, ny, width, PositiveSide, color);

            _triangles.Add(first);
            _triangles.Add(first + 1);
            _triangles.Add(first + 2);
            _triangles.Add(first + 2);
            _triangles.Add(first + 3);
            _triangles.Add(first);
        }

        public void Clear()
        {
            _vertices.Clear();
            _colors.Clear();
            _sides.Clear();
            _triangles.Clear();
        }

        public Mesh Build(string meshName)
        {
            var mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            WriteTo(mesh);

            // Bu çizgiler bir daha değişmez; ana bellekteki kopya bırakılır, sadece grafik belleğinde kalır.
            mesh.UploadMeshData(true);
            return mesh;
        }

        public void WriteTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(_vertices);
            mesh.SetColors(_colors);
            mesh.SetUVs(0, _sides);
            mesh.SetTriangles(_triangles, 0);
        }

        private void AddVertex(float x, float y, float nx, float ny, float width, float side, Color32 color)
        {
            _vertices.Add(new Vector3(x, y, 0f));
            _colors.Add(color);
            _sides.Add(new Vector4(nx, ny, width, side));
        }
    }
}
