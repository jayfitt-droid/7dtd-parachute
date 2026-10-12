using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ParachuteEditor
{
    /// <summary>
    /// Builds the ram-air canopy mesh, its fabric texture and materials, the
    /// suspension lines and the ParachuteCanopy prefab. Everything is generated,
    /// so the Unity project holds no hand-made model. Run it from the Parachute
    /// menu or through BuildBundles.
    ///
    /// Prefab layout the mod's C# relies on: every suspension line ("Line_*") is
    /// a two-point LineRenderer in local space whose point 0 is the canopy end
    /// (-x is the player's left) and whose point 1 is the top of the riser strap
    /// on its side. "Strap_L"/"Strap_R" are the two-point riser straps (point 0
    /// at the shoulder) and "Link_L"/"Link_R" the connector at each strap top.
    /// </summary>
    public static class CanopyBuilder
    {
        public const string Root = "Assets/Parachute";
        public const string MaterialsDir = Root + "/Materials";
        public const string ModelsDir = Root + "/Models";
        public const string PrefabsDir = Root + "/Prefabs";
        public const string TexturesDir = Root + "/Textures";
        public const string AudioDir = Root + "/Audio";
        public const string PrefabPath = PrefabsDir + "/ParachuteCanopy.prefab";
        public const string AudioPath = AudioDir + "/ParachuteDeploy.wav";
        public const string TexturePath = TexturesDir + "/CanopyFabric.png";
        public const string RopeTexturePath = TexturesDir + "/Rope.png";

        // Suspension lines: drawn thicker than real cord so the rope twist reads.
        public const float RopeWidth = 0.024f;
        private const float RopeTilesPerMeter = 9f;

        // Riser straps: webbing from each shoulder up to where the lines gather.
        public const string StrapTexturePath = TexturesDir + "/Strap.png";
        public const float StrapLength = 0.45f;
        public const float StrapWidth = 0.05f;
        private const float StrapTilesPerMeter = 14f;

        // Canopy shape: 7 inflated cells on a circular arc, about 6.7 m across.
        private const int Cells = 7;
        private const int CellSegments = 6;
        private const int ChordSegments = 14;
        private const float ArcLength = 7.2f;
        private const float ArcRadius = 5.5f;
        private const float Chord = 2.8f;
        private const float MaxThickness = 0.32f;
        private const float NoseStart = 0.04f;
        private const float Camber = 0.10f;
        private const float TopBulge = 0.12f;
        private const float BottomBulge = 0.03f;
        private const float StabilizerDepth = 0.36f;
        private const int StabilizerSegments = 10;

        // Chord positions (0 = leading edge, 1 = trailing edge) of the line rows.
        private static readonly float[] LineRows = { 0.06f, 0.30f, 0.55f, 0.85f };

        private static readonly Color Olive = new Color(0.36f, 0.35f, 0.23f);
        private static readonly Color Tan = new Color(0.73f, 0.65f, 0.48f);

        public static void EnsureFolders()
        {
            foreach (string dir in new[] { MaterialsDir, ModelsDir, PrefabsDir, TexturesDir, AudioDir })
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        public static Material CreateMaterial(string name, Color color, float smoothness, bool unlit, Texture2D texture = null)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            Shader shader = unlit ? Shader.Find("Unlit/Color") : Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
            var material = new Material(shader);
            material.name = name;
            if (material.HasProperty("_Color")) material.color = color;
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (texture != null && material.HasProperty("_MainTex")) material.mainTexture = texture;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---- Shape -----------------------------------------------------------

        private static float HalfAngle => ArcLength / ArcRadius * 0.5f;

        private static float Phi(float spanT) { return (spanT - 0.5f) * ArcLength / ArcRadius; }

        private static Vector3 ArcPoint(float phi)
        {
            return new Vector3(ArcRadius * Mathf.Sin(phi), ArcRadius * (Mathf.Cos(phi) - Mathf.Cos(HalfAngle)), 0f);
        }

        private static Vector3 ArcNormal(float phi) { return new Vector3(Mathf.Sin(phi), Mathf.Cos(phi), 0f); }

        /// <summary>Airfoil thickness: a blunt nose, fullest a third back, zero at the tail.</summary>
        private static float Thickness(float s) { return MaxThickness * 2.6f * Mathf.Sqrt(s) * (1f - s); }

        /// <summary>
        /// A point on the canopy skin. spanT runs 0..1 across the whole span, cellT
        /// 0..1 across one cell (0 and 1 are the ribs), s 0..1 along the chord.
        /// </summary>
        private static Vector3 SkinPoint(float spanT, float cellT, float s, bool top)
        {
            float phi = Phi(spanT);
            float thickness = Thickness(s);
            float puff = Mathf.Sin(Mathf.PI * cellT) * Mathf.Clamp01(thickness / MaxThickness * 1.5f);
            float height = Camber * 4f * s * (1f - s);
            height += top ? thickness + TopBulge * puff : -BottomBulge * puff;
            Vector3 point = ArcPoint(phi) + ArcNormal(phi) * height;
            point.z = Chord * (0.5f - s);
            return point;
        }

        private static float ChordS(int j) { return Mathf.Lerp(NoseStart, 1f, j / (float)ChordSegments); }

        private static Mesh BuildMesh()
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var fabric = new List<int>();
            var inlets = new List<int>();
            int columns = CellSegments + 1;
            int rows = ChordSegments + 1;

            // Each cell owns its vertices so the ribs between cells stay creased.
            for (int cell = 0; cell < Cells; cell++)
            {
                int topStart = vertices.Count;
                for (int side = 0; side < 2; side++)
                {
                    for (int i = 0; i < columns; i++)
                    {
                        float cellT = i / (float)CellSegments;
                        float spanT = (cell + cellT) / Cells;
                        for (int j = 0; j < rows; j++)
                        {
                            vertices.Add(SkinPoint(spanT, cellT, ChordS(j), side == 0));
                            uvs.Add(new Vector2(spanT, j / (float)ChordSegments));
                        }
                    }
                }
                int bottomStart = topStart + columns * rows;

                for (int i = 0; i < CellSegments; i++)
                {
                    for (int j = 0; j < ChordSegments; j++)
                    {
                        int a = i * rows + j, b = (i + 1) * rows + j, c = (i + 1) * rows + j + 1, d = i * rows + j + 1;
                        Quad(fabric, topStart + a, topStart + b, topStart + c, topStart + d);
                        Quad(fabric, bottomStart + a, bottomStart + d, bottomStart + c, bottomStart + b);
                    }
                }

                // The open nose of the cell: a dark face between the two skins.
                int inletStart = vertices.Count;
                for (int i = 0; i < columns; i++)
                {
                    vertices.Add(vertices[bottomStart + i * rows]);
                    vertices.Add(vertices[topStart + i * rows]);
                    uvs.Add(Vector2.zero);
                    uvs.Add(Vector2.zero);
                }
                for (int i = 0; i < CellSegments; i++)
                {
                    int bl = inletStart + i * 2, tl = bl + 1, br = bl + 2, tr = bl + 3;
                    Quad(inlets, bl, br, tr, tl);
                }
            }

            AddTip(vertices, uvs, fabric, false);
            AddTip(vertices, uvs, fabric, true);

            var mesh = new Mesh { name = "ParachuteCanopyMesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(fabric, 0);
            mesh.SetTriangles(inlets, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The end rib that closes a wing tip, and the stabilizer panel hanging under it.</summary>
        private static void AddTip(List<Vector3> vertices, List<Vector2> uvs, List<int> fabric, bool right)
        {
            float spanT = right ? 1f : 0f;
            float u = right ? 0.99f : 0.01f;
            int rows = ChordSegments + 1;

            int wall = vertices.Count;
            for (int j = 0; j < rows; j++)
            {
                float s = ChordS(j);
                vertices.Add(SkinPoint(spanT, 0f, s, false));
                vertices.Add(SkinPoint(spanT, 0f, s, true));
                uvs.Add(new Vector2(u, j / (float)ChordSegments));
                uvs.Add(new Vector2(u, j / (float)ChordSegments));
            }
            for (int j = 0; j < ChordSegments; j++)
            {
                int bl = wall + j * 2, tl = bl + 1, bl1 = bl + 2, tl1 = bl + 3;
                if (right) Quad(fabric, bl, bl1, tl1, tl);
                else Quad(fabric, bl, tl, tl1, bl1);
            }

            // Stabilizer: continues the arc outward and down, deepest mid-chord.
            float phi = Phi(spanT);
            Vector3 down = new Vector3(Mathf.Cos(phi) * (right ? 1f : -1f), -Mathf.Sin(Mathf.Abs(phi)), 0f);
            for (int face = 0; face < 2; face++)
            {
                int start = vertices.Count;
                for (int k = 0; k <= StabilizerSegments; k++)
                {
                    float t = k / (float)StabilizerSegments;
                    float s = Mathf.Lerp(NoseStart, 0.8f, t);
                    Vector3 edge = SkinPoint(spanT, 0f, s, false);
                    float depth = StabilizerDepth * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.7f);
                    vertices.Add(edge);
                    vertices.Add(edge + down * depth);
                    uvs.Add(new Vector2(u, s));
                    uvs.Add(new Vector2(u, s));
                }
                for (int k = 0; k < StabilizerSegments; k++)
                {
                    int e = start + k * 2, l = e + 1, e1 = e + 2, l1 = e + 3;
                    if ((face == 0) == right) Quad(fabric, e, e1, l1, l);
                    else Quad(fabric, e, l, l1, e1);
                }
            }
        }

        private static void Quad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(d);
            triangles.Add(b); triangles.Add(c); triangles.Add(d);
        }

        // ---- Fabric texture --------------------------------------------------

        /// <summary>
        /// Paints the canopy fabric: one colour per cell, shaded darker toward the
        /// ribs so the cells read as inflated, with stitched seams, reinforcing
        /// tape at the nose and tail, a ripstop weave and light weathering.
        /// u runs across the span, v from the leading to the trailing edge.
        /// </summary>
        private static Texture2D CreateFabricTexture()
        {
            const int width = 1024;
            const int height = 256;
            var random = new System.Random(20261012);
            const int noiseW = 33, noiseH = 9;
            var noise = new float[noiseW, noiseH];
            for (int x = 0; x < noiseW; x++)
                for (int y = 0; y < noiseH; y++)
                    noise[x, y] = (float)random.NextDouble();

            var pixels = new Color[width * height];
            for (int py = 0; py < height; py++)
            {
                float v = (py + 0.5f) / height;
                for (int px = 0; px < width; px++)
                {
                    float u = (px + 0.5f) / width;
                    float cellPos = u * Cells;
                    int cell = Mathf.Min(Cells - 1, (int)cellPos);
                    float cellT = cellPos - cell;
                    Color color = cell % 2 == 0 ? Olive : Tan;

                    float fromRib = Mathf.Min(cellT, 1f - cellT);
                    float shade = 1f - 0.25f * Mathf.Pow(1f - 2f * fromRib, 3f);
                    if (fromRib < 0.02f) shade *= 0.55f;
                    if (v < 0.05f || v > 0.97f) shade *= 0.72f;
                    if (Mathf.Abs(v - 0.36f) < 0.006f || Mathf.Abs(v - 0.68f) < 0.006f) shade *= 0.82f;
                    if (px % 16 == 0 || py % 16 == 0) shade *= 0.94f;

                    float nx = u * (noiseW - 1), ny = v * (noiseH - 1);
                    int x0 = Mathf.Min(noiseW - 2, (int)nx), y0 = Mathf.Min(noiseH - 2, (int)ny);
                    float fx = nx - x0, fy = ny - y0;
                    float grime = Mathf.Lerp(
                        Mathf.Lerp(noise[x0, y0], noise[x0 + 1, y0], fx),
                        Mathf.Lerp(noise[x0, y0 + 1], noise[x0 + 1, y0 + 1], fx), fy);
                    shade *= 0.9f + 0.16f * grime;
                    shade *= 0.98f + 0.04f * (float)random.NextDouble();

                    pixels[py * width + px] = new Color(color.r * shade, color.g * shade, color.b * shade, 1f);
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return SaveTexture(texture, TexturePath, TextureWrapMode.Clamp);
        }

        /// <summary>
        /// Paints one repeat of dark twisted rope: two diagonal strands per tile,
        /// rounded across the width so the flat line reads as a cord. u runs
        /// along the rope and tiles, v runs across it.
        /// </summary>
        private static Texture2D CreateRopeTexture()
        {
            const int width = 128;
            const int height = 32;
            var random = new System.Random(20261013);
            var rope = new Color(0.30f, 0.27f, 0.22f);
            var pixels = new Color[width * height];
            for (int py = 0; py < height; py++)
            {
                float v = (py + 0.5f) / height;
                float across = 2f * v - 1f;
                float round = Mathf.Sqrt(Mathf.Max(0f, 1f - across * across));
                for (int px = 0; px < width; px++)
                {
                    float u = (px + 0.5f) / width;
                    float phase = Mathf.Repeat(u * 2f + v * 0.8f, 1f);
                    float strand = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * phase)), 0.7f);
                    float shade = (0.25f + 0.75f * strand) * (0.3f + 0.7f * round);
                    shade *= 0.9f + 0.2f * (float)random.NextDouble();
                    pixels[py * width + px] = new Color(rope.r * shade, rope.g * shade, rope.b * shade, 1f);
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return SaveTexture(texture, RopeTexturePath, TextureWrapMode.Repeat);
        }

        /// <summary>
        /// Paints one repeat of dark nylon webbing: fine ribs across the strap
        /// with a stitched, slightly raised edge on both sides. u runs along the
        /// strap and tiles, v runs across it.
        /// </summary>
        private static Texture2D CreateStrapTexture()
        {
            const int width = 32;
            const int height = 32;
            var random = new System.Random(20261014);
            var webbing = new Color(0.20f, 0.20f, 0.15f);
            var pixels = new Color[width * height];
            for (int py = 0; py < height; py++)
            {
                float v = (py + 0.5f) / height;
                float fromEdge = Mathf.Min(v, 1f - v);
                for (int px = 0; px < width; px++)
                {
                    float u = (px + 0.5f) / width;
                    float shade = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * u * 4f);
                    if (fromEdge < 0.16f) shade *= 1.15f;
                    if (fromEdge < 0.05f) shade *= 0.5f;
                    if (Mathf.Abs(fromEdge - 0.16f) < 0.025f && Mathf.Repeat(u * 4f, 1f) < 0.5f) shade *= 0.6f;
                    shade *= 0.92f + 0.16f * (float)random.NextDouble();
                    pixels[py * width + px] = new Color(webbing.r * shade, webbing.g * shade, webbing.b * shade, 1f);
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return SaveTexture(texture, StrapTexturePath, TextureWrapMode.Repeat);
        }

        private static Texture2D SaveTexture(Texture2D texture, string path, TextureWrapMode wrap)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = wrap;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---- Prefab ----------------------------------------------------------

        public static GameObject CreateCanopy(Material fabric, Material inlet, Material lines, Material strap, Material metal)
        {
            Mesh mesh = BuildMesh();
            AssetDatabase.CreateAsset(mesh, ModelsDir + "/ParachuteCanopyMesh.asset");

            var root = new GameObject("ParachuteCanopy");

            var canopy = new GameObject("Canopy");
            canopy.transform.SetParent(root.transform, false);
            canopy.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = canopy.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { fabric, inlet };

            // A riser strap rises from each shoulder toward its half of the
            // canopy, and every suspension line on that side runs from its rib
            // down to the top of that strap. The canopy root sits one
            // canopy_height (default 3.2 m) above the player's feet, so the
            // shoulder here is a fixed local offset; the mod re-aims the straps
            // and lines every frame.
            var strapTops = new Vector3[2];
            for (int side = 0; side < 2; side++)
            {
                Vector3 centroid = Vector3.zero;
                int count = 0;
                for (int rib = 0; rib <= Cells; rib++)
                {
                    foreach (float row in LineRows)
                    {
                        Vector3 anchor = SkinPoint(rib / (float)Cells, 0f, row, false);
                        if ((anchor.x < 0f) != (side == 0)) continue;
                        centroid += anchor;
                        count++;
                    }
                }
                centroid /= count;

                var shoulder = new Vector3(side == 0 ? -0.25f : 0.25f, -1.8f, -0.1f);
                strapTops[side] = shoulder + (centroid - shoulder).normalized * StrapLength;
                string suffix = side == 0 ? "L" : "R";

                var strapObject = new GameObject("Strap_" + suffix);
                strapObject.transform.SetParent(root.transform, false);
                var strapLine = strapObject.AddComponent<LineRenderer>();
                strapLine.useWorldSpace = false;
                strapLine.positionCount = 2;
                strapLine.SetPosition(0, shoulder);
                strapLine.SetPosition(1, strapTops[side]);
                strapLine.startWidth = StrapWidth;
                strapLine.endWidth = StrapWidth;
                strapLine.textureMode = LineTextureMode.Tile;
                strapLine.generateLightingData = true;
                strapLine.sharedMaterial = strap;
                strapLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                strapLine.receiveShadows = false;

                GameObject link = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                link.name = "Link_" + suffix;
                Object.DestroyImmediate(link.GetComponent<Collider>());
                link.transform.SetParent(root.transform, false);
                link.transform.localPosition = strapTops[side];
                link.transform.localScale = Vector3.one * 0.07f;
                var linkRenderer = link.GetComponent<MeshRenderer>();
                linkRenderer.sharedMaterial = metal;
                linkRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            for (int rib = 0; rib <= Cells; rib++)
            {
                float spanT = rib / (float)Cells;
                for (int row = 0; row < LineRows.Length; row++)
                {
                    Vector3 anchor = SkinPoint(spanT, 0f, LineRows[row], false);
                    Vector3 shoulder = strapTops[anchor.x < 0f ? 0 : 1];

                    var riser = new GameObject("Line_" + rib + "_" + row);
                    riser.transform.SetParent(root.transform, false);
                    var line = riser.AddComponent<LineRenderer>();
                    line.useWorldSpace = false;
                    line.positionCount = 2;
                    line.SetPosition(0, anchor);
                    line.SetPosition(1, shoulder);
                    line.startWidth = RopeWidth;
                    line.endWidth = RopeWidth;
                    line.textureMode = LineTextureMode.Tile;
                    line.generateLightingData = true;
                    line.sharedMaterial = lines;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    line.receiveShadows = false;
                }
            }

            return root;
        }

        [MenuItem("Parachute/Build Canopy Assets")]
        public static void BuildMenu()
        {
            Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Parachute] Canopy assets built.");
        }

        public static void Build()
        {
            EnsureFolders();
            ConfigureAudioImporter();
            AssetDatabase.DeleteAsset(MaterialsDir + "/CanopyOlive.mat");
            AssetDatabase.DeleteAsset(MaterialsDir + "/CanopyTan.mat");

            Texture2D fabricTexture = CreateFabricTexture();
            Material fabric = CreateMaterial("CanopyFabric", Color.white, 0.12f, false, fabricTexture);
            Material inlet = CreateMaterial("CanopyInlet", new Color(0.07f, 0.07f, 0.06f), 0.0f, false);
            Material lines = CreateMaterial("Lines", Color.white, 0.05f, false, CreateRopeTexture());
            lines.mainTextureScale = new Vector2(RopeTilesPerMeter, 1f);
            EditorUtility.SetDirty(lines);

            Material strap = CreateMaterial("Strap", Color.white, 0.05f, false, CreateStrapTexture());
            strap.mainTextureScale = new Vector2(StrapTilesPerMeter, 1f);
            EditorUtility.SetDirty(strap);
            Material metal = CreateMaterial("Link", new Color(0.22f, 0.22f, 0.21f), 0.55f, false);

            GameObject root = CreateCanopy(fabric, inlet, lines, strap, metal);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigureAudioImporter()
        {
            if (!File.Exists(AudioPath)) return;
            AssetDatabase.ImportAsset(AudioPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(AudioPath) as AudioImporter;
            if (importer == null) return;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.SaveAndReimport();
        }
    }
}
