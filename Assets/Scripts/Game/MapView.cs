using GeoWar.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeoWar.Game
{
    // Haritayı katmanlar halinde çizer: ızgara, kıyı parıltısı, dolgu, il sınırları, ülke sınırları, seçim.
    // Kaydırma ve yakınlaştırma sadece kamerayı oynatır; çizgi kalınlığını gölgelendirici sabit tutar.
    public sealed class MapView : MonoBehaviour
    {
        private const string FillShaderName = "GeoWarVertexColor";
        private const string LineShaderName = "GeoWarScreenLine";
        private const string LineScaleProperty = "_GeoWarLineScale";

        private const int GridOrder = 0;
        private const int CoastOrder = 1;
        private const int FillOrder = 2;
        private const int ProvinceLineOrder = 3;
        private const int CountryLineOrder = 4;
        private const int SelectionOrder = 5;

        private const int GridStepDegrees = 5;
        private const int GridMinLon = 0;
        private const int GridMaxLon = 75;
        private const int GridMinLat = 5;
        private const int GridMaxLat = 70;

        private MapData _map;
        private Mesh _fillMesh;
        private Mesh _selectionMesh;
        private Color32[] _baseColors;
        private Color32[] _colors;
        private Material _lineMaterial;
        private LineMeshBuilder _selectionLines;
        private int _selectedProvince = MapHitTest.NotFound;

        public int VertexCount { get; private set; }
        public int TriangleCount { get; private set; }

        public void Build(MapData map, CountryRelation[] relations)
        {
            _map = map;
            ApplyLineScale();

            var fillMaterial = new Material(Resources.Load<Shader>(FillShaderName));
            _lineMaterial = new Material(Resources.Load<Shader>(LineShaderName));

            BuildFill(fillMaterial, relations);
            AddLineLayer("Grid", BuildGrid(), GridOrder);
            AddLineLayer("CoastGlow", BuildLines(map.CoastLines, MapPalette.CoastGlow, MapPalette.CoastGlowWidth), CoastOrder);
            AddLineLayer("ProvinceLines", BuildProvinceLines(relations), ProvinceLineOrder);
            AddLineLayer("CountryLines", BuildLines(map.CountryBorderLines, MapPalette.CountryLine, MapPalette.CountryLineWidth), CountryLineOrder);

            _selectionLines = new LineMeshBuilder();
            _selectionMesh = new Mesh { name = "Selection", indexFormat = IndexFormat.UInt32 };
            _selectionMesh.MarkDynamic();
            AddRenderer("Selection", _selectionMesh, _lineMaterial, SelectionOrder);
        }

        // Çizgi kalınlığı ekran ölçeğine bağlıdır; pencere boyutu değişince yeniden ayarlanır.
        public void ApplyLineScale()
        {
            Shader.SetGlobalFloat(LineScaleProperty, MapLayout.UiScale);
        }

        // Seçilen il altın rengine boyanır ve çerçevelenir; sadece dokunuşta çalışır, her karede değil.
        public void Select(int province)
        {
            if (_selectedProvince != MapHitTest.NotFound)
            {
                int start = _map.VertexStart[_selectedProvince];
                int count = _map.VertexStart[_selectedProvince + 1] - start;
                System.Array.Copy(_baseColors, start, _colors, start, count);
            }

            _selectedProvince = province;
            _selectionLines.Clear();

            if (province != MapHitTest.NotFound)
            {
                int end = _map.VertexStart[province + 1];
                for (int vertex = _map.VertexStart[province]; vertex < end; vertex++)
                {
                    _colors[vertex] = MapPalette.Selected;
                }

                AddSelectionOutline(province);
            }

            _fillMesh.colors32 = _colors;
            _selectionLines.WriteTo(_selectionMesh);
        }

        private void AddSelectionOutline(int province)
        {
            int ringEnd = _map.ProvinceRingStart[province + 1];
            for (int ring = _map.ProvinceRingStart[province]; ring < ringEnd; ring++)
            {
                int start = _map.RingStart[ring];
                int end = _map.RingStart[ring + 1];
                for (int i = start, j = end - 1; i < end; j = i, i++)
                {
                    _selectionLines.Add(
                        _map.VertexX[j],
                        _map.VertexY[j],
                        _map.VertexX[i],
                        _map.VertexY[i],
                        MapPalette.SelectedLine,
                        MapPalette.SelectedLineWidth);
                }
            }
        }

        private void BuildFill(Material material, CountryRelation[] relations)
        {
            int vertexCount = _map.VertexX.Length;
            var vertices = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i] = new Vector3(_map.VertexX[i], _map.VertexY[i], 0f);
            }

            _baseColors = BuildProvinceColors(relations, vertexCount);
            _colors = (Color32[])_baseColors.Clone();

            _fillMesh = new Mesh { name = "MapFill", indexFormat = IndexFormat.UInt32 };
            // Seçimde sadece dolgunun renkleri değişir.
            _fillMesh.MarkDynamic();
            _fillMesh.vertices = vertices;
            _fillMesh.colors32 = _colors;
            _fillMesh.SetIndices(_map.Triangles, MeshTopology.Triangles, 0);
            AddRenderer("MapFill", _fillMesh, material, FillOrder);

            VertexCount += vertexCount;
            TriangleCount += _map.Triangles.Length / 3;
        }

        private Color32[] BuildProvinceColors(CountryRelation[] relations, int vertexCount)
        {
            var colors = new Color32[vertexCount];
            for (int province = 0; province < _map.ProvinceCount; province++)
            {
                byte country = _map.ProvinceCountry[province];
                Color32 color = country == MapData.NoCountry
                    ? MapPalette.Land
                    : MapPalette.Fill(relations[country], _map.ProvinceTone[province]);

                int end = _map.VertexStart[province + 1];
                for (int vertex = _map.VertexStart[province]; vertex < end; vertex++)
                {
                    colors[vertex] = color;
                }
            }

            return colors;
        }

        private LineMeshBuilder BuildGrid()
        {
            var lines = new LineMeshBuilder();
            float minX = GridMinLon * MapLayout.LonScale;
            float maxX = GridMaxLon * MapLayout.LonScale;
            float minY = GridMinLat * MapLayout.LatScale;
            float maxY = GridMaxLat * MapLayout.LatScale;

            for (int lon = GridMinLon; lon <= GridMaxLon; lon += GridStepDegrees)
            {
                float x = lon * MapLayout.LonScale;
                lines.Add(x, minY, x, maxY, MapPalette.Grid, MapPalette.GridWidth);
            }

            for (int lat = GridMinLat; lat <= GridMaxLat; lat += GridStepDegrees)
            {
                float y = lat * MapLayout.LatScale;
                lines.Add(minX, y, maxX, y, MapPalette.Grid, MapPalette.GridWidth);
            }

            return lines;
        }

        private LineMeshBuilder BuildLines(int[] edges, Color32 color, float width)
        {
            var lines = new LineMeshBuilder();
            for (int i = 0; i < edges.Length; i += 2)
            {
                int a = edges[i];
                int b = edges[i + 1];
                lines.Add(_map.VertexX[a], _map.VertexY[a], _map.VertexX[b], _map.VertexY[b], color, width);
            }

            return lines;
        }

        // İl sınırı, ilin ülkesiyle ilişkiye göre renk alır.
        private LineMeshBuilder BuildProvinceLines(CountryRelation[] relations)
        {
            var vertexProvince = new int[_map.VertexX.Length];
            for (int province = 0; province < _map.ProvinceCount; province++)
            {
                int end = _map.VertexStart[province + 1];
                for (int vertex = _map.VertexStart[province]; vertex < end; vertex++)
                {
                    vertexProvince[vertex] = province;
                }
            }

            var lines = new LineMeshBuilder();
            int[] edges = _map.ProvinceBorderLines;
            for (int i = 0; i < edges.Length; i += 2)
            {
                int a = edges[i];
                int b = edges[i + 1];
                CountryRelation relation = relations[_map.ProvinceCountry[vertexProvince[a]]];
                lines.Add(
                    _map.VertexX[a],
                    _map.VertexY[a],
                    _map.VertexX[b],
                    _map.VertexY[b],
                    MapPalette.ProvinceLine(relation),
                    MapPalette.ProvinceLineWidth(relation));
            }

            return lines;
        }

        private void AddLineLayer(string layerName, LineMeshBuilder lines, int order)
        {
            VertexCount += lines.VertexCount;
            TriangleCount += lines.TriangleCount;
            AddRenderer(layerName, lines.Build(layerName), _lineMaterial, order);
        }

        private void AddRenderer(string childName, Mesh mesh, Material material, int order)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = child.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = order;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }
    }
}
