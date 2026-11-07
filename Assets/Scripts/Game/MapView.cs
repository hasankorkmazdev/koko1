using System.Collections.Generic;
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
        private const string ProvinceLinesName = "ProvinceLines";

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
        private CountryRelation[] _relations;
        private int[] _provinceOwner;
        private Mesh _fillMesh;
        private Mesh _selectionMesh;
        private MeshFilter _provinceLines;
        private Mesh _rebelBorderMesh;
        private Color32[] _baseColors;
        private Color32[] _colors;
        private Material _lineMaterial;
        private LineMeshBuilder _selectionLines;
        private int _selectedProvince = MapHitTest.NotFound;

        public int VertexCount { get; private set; }
        public int TriangleCount { get; private set; }

        public void Build(MapData map, CountryRelation[] relations)
        {
            Build(map, relations, GameSession.StartingOwners(map));
        }

        public void Build(MapData map, CountryRelation[] relations, int[] provinceOwner)
        {
            _map = map;
            _relations = relations;
            _provinceOwner = provinceOwner;
            ApplyLineScale();

            var fillMaterial = new Material(Resources.Load<Shader>(FillShaderName));
            _lineMaterial = new Material(Resources.Load<Shader>(LineShaderName));

            BuildFill(fillMaterial, relations);
            AddLineLayer("Grid", BuildGrid(), GridOrder);
            AddLineLayer("CoastGlow", BuildLines(map.CoastLines, MapPalette.CoastGlow, MapPalette.CoastGlowWidth), CoastOrder);
            _provinceLines = AddLineLayer(ProvinceLinesName, BuildProvinceLines(relations), ProvinceLineOrder);
            AddLineLayer("CountryLines", BuildLines(map.CountryBorderLines, MapPalette.CountryLine, MapPalette.CountryLineWidth), CountryLineOrder);

            _rebelBorderMesh = new Mesh { name = "RebelBorders", indexFormat = IndexFormat.UInt32 };
            AddRenderer("RebelBorders", _rebelBorderMesh, _lineMaterial, CountryLineOrder);

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
                PaintSelected(province);
                AddSelectionOutline(province);
            }

            _fillMesh.colors32 = _colors;
            _selectionLines.WriteTo(_selectionMesh);
        }

        // Şehir el değiştirince renkler, il çizgileri ve isyancı devletin sınırı yeniden çizilir. Yalnızca o anda çalışır.
        public void RefreshOwners()
        {
            _baseColors = BuildProvinceColors(_relations, _baseColors.Length);
            System.Array.Copy(_baseColors, _colors, _colors.Length);
            if (_selectedProvince != MapHitTest.NotFound)
            {
                PaintSelected(_selectedProvince);
            }

            _fillMesh.colors32 = _colors;
            BuildRebelBorders().WriteTo(_rebelBorderMesh);

            // Çizgi mesh'inin ana bellekteki kopyası bırakıldığı için üstüne yazılamaz; yenisi kurulur.
            Mesh oldLines = _provinceLines.sharedMesh;
            _provinceLines.sharedMesh = BuildProvinceLines(_relations).Build(ProvinceLinesName);
            Destroy(oldLines);
        }

        private void PaintSelected(int province)
        {
            int end = _map.VertexStart[province + 1];
            for (int vertex = _map.VertexStart[province]; vertex < end; vertex++)
            {
                _colors[vertex] = MapPalette.Selected;
            }
        }

        // Aynı devletin iki iline ait kenar iç kenardır, kıyı kenarı da sınır sayılmaz; kalanlar kalın çizilir.
        private LineMeshBuilder BuildRebelBorders()
        {
            var coast = new HashSet<(float, float, float, float)>();
            for (int i = 0; i < _map.CoastLines.Length; i += 2)
            {
                coast.Add(EdgeKey(_map.CoastLines[i], _map.CoastLines[i + 1]));
            }

            var edges = new Dictionary<((float, float, float, float) Edge, int Owner), int>();
            for (int province = 0; province < _map.ProvinceCount; province++)
            {
                int owner = _provinceOwner[province];
                if (owner == MapData.NoCountry || owner < _map.Countries.Length)
                {
                    continue;
                }

                int ringEnd = _map.ProvinceRingStart[province + 1];
                for (int ring = _map.ProvinceRingStart[province]; ring < ringEnd; ring++)
                {
                    int start = _map.RingStart[ring];
                    int end = _map.RingStart[ring + 1];
                    for (int i = start, j = end - 1; i < end; j = i, i++)
                    {
                        var key = (EdgeKey(i, j), owner);
                        edges.TryGetValue(key, out int count);
                        edges[key] = count + 1;
                    }
                }
            }

            var lines = new LineMeshBuilder();
            foreach (KeyValuePair<((float, float, float, float) Edge, int Owner), int> edge in edges)
            {
                if (edge.Value == 1 && !coast.Contains(edge.Key.Edge))
                {
                    (float ax, float ay, float bx, float by) = edge.Key.Edge;
                    lines.Add(ax, ay, bx, by, MapPalette.CountryLine, MapPalette.CountryLineWidth);
                }
            }

            return lines;
        }

        private (float, float, float, float) EdgeKey(int a, int b)
        {
            float ax = _map.VertexX[a];
            float ay = _map.VertexY[a];
            float bx = _map.VertexX[b];
            float by = _map.VertexY[b];
            bool swap = ax > bx || (ax == bx && ay > by);
            return swap ? (bx, by, ax, ay) : (ax, ay, bx, by);
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
                int country = _provinceOwner[province];
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
                CountryRelation relation = relations[_provinceOwner[vertexProvince[a]]];
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

        private MeshFilter AddLineLayer(string layerName, LineMeshBuilder lines, int order)
        {
            VertexCount += lines.VertexCount;
            TriangleCount += lines.TriangleCount;
            return AddRenderer(layerName, lines.Build(layerName), _lineMaterial, order);
        }

        private MeshFilter AddRenderer(string childName, Mesh mesh, Material material, int order)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            var meshFilter = child.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            var meshRenderer = child.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = order;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return meshFilter;
        }
    }
}
