using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ParachuteEditor
{
    /// <summary>
    /// Builds the worn sport-rig container: a flat pack high on the back with the
    /// reserve tray on top and the main container below, two shoulder straps over
    /// the shoulders and down the chest to the waist, hip straps from there round
    /// to the bottom of the container, a chest strap and a pilot-chute handle.
    /// Everything is generated, like the canopy. Run it from the Parachute menu or
    /// through BuildBundles.
    ///
    /// Prefab layout the mod's C# relies on: the root's axes are the body's (x to
    /// the player's right, y up the spine, z out of the chest) and its origin is
    /// the middle of the upper back, on the back's surface; the pack itself lies
    /// at negative z. "StrapTop_L"/"StrapTop_R" are empties on top of each
    /// shoulder strap, where the canopy's riser straps start. "Front" groups the
    /// pieces on the chest; the mod scales it along z for pack_chest_depth. No
    /// colliders.
    /// </summary>
    public static class PackBuilder
    {
        public const string PrefabPath = CanopyBuilder.PrefabsDir + "/ParachutePack.prefab";
        public const string PackTexturePath = CanopyBuilder.TexturesDir + "/PackFabric.png";

        private const float StrapWidth = 0.045f;
        private const float StrapThickness = 0.006f;
        private const float StrapTilesPerMeter = 14f;

        // Superellipsoid exponent: small is boxy, 1 is a sphere.
        private const float Boxiness = 0.22f;
        private const int Rings = 16;
        private const int Segments = 32;

        private static readonly Color Olive = new Color(0.30f, 0.30f, 0.20f);
        private static readonly Color Tan = new Color(0.62f, 0.55f, 0.40f);

        // ---- Shapes ----------------------------------------------------------

        /// <summary>
        /// Values within rounding of zero count as zero: cos(90°) comes out as
        /// about -4e-8, and a small power turns that into -0.02, which folded a
        /// little mirrored ring into each pole of the box.
        /// </summary>
        private static float SignedPow(float value, float power)
        {
            if (Mathf.Abs(value) < 1e-5f) return 0f;
            return Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), power);
        }

        /// <summary>
        /// A rounded box: a superellipsoid with the given centre and half sizes.
        /// u runs round it, v from the bottom to the top.
        /// </summary>
        private static Mesh RoundedBox(string name, Vector3 center, Vector3 half)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= Rings; i++)
            {
                float eta = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, i / (float)Rings);
                for (int j = 0; j <= Segments; j++)
                {
                    float omega = Mathf.Lerp(-Mathf.PI, Mathf.PI, j / (float)Segments);
                    float ring = SignedPow(Mathf.Cos(eta), Boxiness);
                    var point = new Vector3(
                        half.x * ring * SignedPow(Mathf.Cos(omega), Boxiness),
                        half.y * SignedPow(Mathf.Sin(eta), Boxiness),
                        half.z * ring * SignedPow(Mathf.Sin(omega), Boxiness));
                    vertices.Add(center + point);
                    uvs.Add(new Vector2(j / (float)Segments, i / (float)Rings));
                }
            }
            int columns = Segments + 1;
            for (int i = 0; i < Rings; i++)
            {
                for (int j = 0; j < Segments; j++)
                {
                    int a = i * columns + j, b = a + 1, c = a + columns + 1, d = a + columns;
                    triangles.Add(a); triangles.Add(d); triangles.Add(b);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }
            FaceOutward(vertices, triangles, center);

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Reverses the winding if the closed surface would face inward.</summary>
        private static void FaceOutward(List<Vector3> vertices, List<int> triangles, Vector3 center)
        {
            float volume = 0f;
            for (int t = 0; t < triangles.Count; t += 3)
            {
                Vector3 a = vertices[triangles[t]] - center;
                Vector3 b = vertices[triangles[t + 1]] - center;
                Vector3 c = vertices[triangles[t + 2]] - center;
                volume += Vector3.Dot(a, Vector3.Cross(b, c));
            }
            // A front face's normal is Cross(b - a, c - a) (RecalculateNormals), so a
            // closed surface whose faces point outward has a positive signed volume.
            // The first build had this backwards and showed the inside of the box.
            if (volume > 0f) return;
            for (int t = 0; t < triangles.Count; t += 3)
            {
                int swap = triangles[t + 1];
                triangles[t + 1] = triangles[t + 2];
                triangles[t + 2] = swap;
            }
        }

        private const int PointsPerSpan = 6;

        /// <summary>A Catmull-Rom curve through the path; path point k lands at index k * PointsPerSpan.</summary>
        private static List<Vector3> Smooth(Vector3[] path)
        {
            var points = new List<Vector3>();
            for (int k = 0; k < path.Length - 1; k++)
            {
                Vector3 p0 = path[Mathf.Max(0, k - 1)], p1 = path[k], p2 = path[k + 1], p3 = path[Mathf.Min(path.Length - 1, k + 2)];
                for (int s = 0; s < PointsPerSpan; s++)
                {
                    float t = s / (float)PointsPerSpan;
                    points.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
                        (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
                }
            }
            points.Add(path[path.Length - 1]);
            return points;
        }

        /// <summary>
        /// Flat webbing along a curve, with a face on each side so it reads from
        /// front and back. side is the direction across the strap. u runs along the
        /// strap in metres (the strap material tiles per metre), v across it.
        /// </summary>
        private static Mesh Webbing(string name, List<Vector3> points, Vector3 side)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int face = 0; face < 2; face++)
            {
                int start = vertices.Count;
                float along = 0f;
                for (int k = 0; k < points.Count; k++)
                {
                    if (k > 0) along += Vector3.Distance(points[k - 1], points[k]);
                    Vector3 tangent = (points[Mathf.Min(points.Count - 1, k + 1)] - points[Mathf.Max(0, k - 1)]).normalized;
                    Vector3 across = Vector3.ProjectOnPlane(side, tangent).normalized;
                    Vector3 normal = Vector3.Cross(tangent, across) * (face == 0 ? 1f : -1f);
                    Vector3 middle = points[k] + normal * (StrapThickness * 0.5f);
                    vertices.Add(middle - across * (StrapWidth * 0.5f));
                    vertices.Add(middle + across * (StrapWidth * 0.5f));
                    uvs.Add(new Vector2(along, 0f));
                    uvs.Add(new Vector2(along, 1f));
                }
                for (int k = 0; k < points.Count - 1; k++)
                {
                    // Unity's front face is the clockwise one, whose normal is
                    // Cross(b - a, d - a); face 0 has to face +normal, face 1 -normal.
                    int a = start + k * 2, b = a + 1, c = a + 3, d = a + 2;
                    if (face == 1)
                    {
                        triangles.Add(a); triangles.Add(b); triangles.Add(d);
                        triangles.Add(b); triangles.Add(c); triangles.Add(d);
                    }
                    else
                    {
                        triangles.Add(a); triangles.Add(d); triangles.Add(b);
                        triangles.Add(b); triangles.Add(d); triangles.Add(c);
                    }
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // The front pieces (both straps below their tops, the hip straps, the chest
        // strap and the buckles) hang from a "Front" group whose origin is at the strap tops' depth.
        // The mod scales that group along z (pack_chest_depth) to fit the chest of
        // whatever armor is worn, and the strap tops stay put.
        private const float FrontPivotZ = 0.10f;

        // How deep the chest is in front of the back, at default pack_chest_depth.
        // Sized for a coat over a chest outfit (2026-10-16 screenshots).
        private const float ChestZ = 0.31f;

        /// <summary>Index of the strap top in ShoulderPath; the strap is split there.</summary>
        private const int TopIndex = 2;

        /// <summary>The left shoulder strap; the right one is its mirror (x negated).</summary>
        private static Vector3[] ShoulderPath(float sign)
        {
            var path = new[]
            {
                new Vector3(-0.10f, 0.10f, -0.01f),
                new Vector3(-0.11f, 0.17f, 0.03f),
                new Vector3(-0.12f, 0.20f, FrontPivotZ),
                new Vector3(-0.12f, 0.18f, 0.20f),
                new Vector3(-0.115f, 0.08f, ChestZ - 0.02f),
                new Vector3(-0.11f, -0.12f, ChestZ),
                new Vector3(-0.115f, -0.30f, ChestZ - 0.03f),
                HipFront(1f),
            };
            for (int i = 0; i < path.Length; i++) path[i].x *= sign;
            return path;
        }

        // Where each shoulder strap meets its hip strap, at the front of the waist.
        private const float HipY = -0.40f;

        private static Vector3 HipFront(float sign)
        {
            return new Vector3(-0.12f * sign, HipY, ChestZ - 0.05f);
        }

        /// <summary>
        /// The left hip strap, from the bottom of the shoulder strap round the side
        /// of the waist to the bottom corner of the container; the right one is its
        /// mirror. The first strap stopped at mid-chest (Jason's screenshots,
        /// 2026-10-16), which read as a harness with its bottom missing.
        /// </summary>
        private static Vector3[] HipPath(float sign)
        {
            var path = new[]
            {
                HipFront(1f),
                new Vector3(-0.19f, HipY, 0.15f),
                new Vector3(-0.20f, HipY + 0.02f, 0.03f),
                new Vector3(-0.15f, -0.33f, -0.02f),
            };
            for (int i = 0; i < path.Length; i++) path[i].x *= sign;
            return path;
        }

        /// <summary>Where the riser strap starts: the top of the shoulder strap, on its outer face.</summary>
        private static Vector3 StrapTop(float sign)
        {
            return new Vector3(-0.12f * sign, 0.20f + StrapThickness, FrontPivotZ + 0.01f);
        }

        // ---- Fabric texture --------------------------------------------------

        /// <summary>
        /// Paints Cordura nylon: a fine basket weave with light weathering and a
        /// stitched binding band where the rounded edges turn, so the pack's faces
        /// read as panels. u runs round the box, v from the bottom to the top.
        /// </summary>
        private static Texture2D CreatePackTexture()
        {
            const int size = 256;
            var random = new System.Random(20261016);
            var pixels = new Color[size * size];
            for (int py = 0; py < size; py++)
            {
                float v = (py + 0.5f) / size;
                for (int px = 0; px < size; px++)
                {
                    float u = (px + 0.5f) / size;
                    float shade = 1f;
                    if (((px / 2) + (py / 2)) % 2 == 0) shade *= 0.93f;
                    // The rounded rims sit about a sixth of the way in from each pole,
                    // and at a quarter turn round from the back and front faces.
                    float rim = Mathf.Min(Mathf.Abs(v - 0.17f), Mathf.Abs(v - 0.83f));
                    float side = Mathf.Min(Mathf.Abs(Mathf.Repeat(u, 0.5f) - 0.125f), Mathf.Abs(Mathf.Repeat(u, 0.5f) - 0.375f));
                    if (rim < 0.03f || side < 0.015f) shade *= 0.72f;
                    if ((Mathf.Abs(rim - 0.03f) < 0.006f) && px % 6 < 3) shade *= 0.6f;
                    shade *= 0.9f + 0.18f * (float)random.NextDouble();
                    pixels[py * size + px] = new Color(shade, shade, shade, 1f);
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(PackTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(PackTexturePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(PackTexturePath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(PackTexturePath);
        }

        // ---- Prefab ----------------------------------------------------------

        /// <summary>
        /// A mesh part. Meshes are authored in the pack root's space; a part under a
        /// group is shifted back by the group's position so it lands in the same place.
        /// </summary>
        private static GameObject Part(Transform parent, string name, Mesh mesh, Material material)
        {
            AssetDatabase.CreateAsset(mesh, CanopyBuilder.ModelsDir + "/" + mesh.name + ".asset");
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = -parent.localPosition;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        /// <summary>A small sphere at a position in the pack root's space.</summary>
        private static void Knob(Transform parent, string name, Material material, Vector3 position, Vector3 size)
        {
            GameObject knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name = name;
            Object.DestroyImmediate(knob.GetComponent<Collider>());
            knob.transform.SetParent(parent, false);
            knob.transform.localPosition = position - parent.localPosition;
            knob.transform.localScale = size;
            knob.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        public static GameObject CreatePack(Material main, Material reserve, Material strap, Material metal, Material handle)
        {
            var root = new GameObject("ParachutePack");

            Part(root.transform, "Main", RoundedBox("ParachutePackMain", new Vector3(0f, -0.15f, -0.07f), new Vector3(0.17f, 0.19f, 0.07f)), main);
            Part(root.transform, "Reserve", RoundedBox("ParachutePackReserve", new Vector3(0f, 0.08f, -0.075f), new Vector3(0.165f, 0.065f, 0.075f)), reserve);

            var front = new GameObject("Front");
            front.transform.SetParent(root.transform, false);
            front.transform.localPosition = new Vector3(0f, 0f, FrontPivotZ);

            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? 1f : -1f;
                string suffix = side == 0 ? "L" : "R";
                List<Vector3> points = Smooth(ShoulderPath(sign));
                int split = TopIndex * PointsPerSpan;
                Part(root.transform, "ShoulderStrap_" + suffix,
                    Webbing("ParachutePackStrap" + suffix, points.GetRange(0, split + 1), Vector3.right), strap);
                Part(front.transform, "FrontStrap_" + suffix,
                    Webbing("ParachutePackFrontStrap" + suffix, points.GetRange(split, points.Count - split), Vector3.right), strap);
                Part(front.transform, "HipStrap_" + suffix,
                    Webbing("ParachutePackHipStrap" + suffix, Smooth(HipPath(sign)), Vector3.up), strap);
                Knob(front.transform, "HipRing_" + suffix, metal, HipFront(sign) + new Vector3(0f, 0f, 0.008f), new Vector3(0.05f, 0.05f, 0.014f));

                var top = new GameObject("StrapTop_" + suffix);
                top.transform.SetParent(root.transform, false);
                top.transform.localPosition = StrapTop(sign);
                Knob(root.transform, "Ring_" + suffix, metal, StrapTop(sign), new Vector3(0.05f, 0.012f, 0.03f));
            }

            float chestStrapZ = ChestZ - 0.004f;
            Part(front.transform, "ChestStrap",
                Webbing("ParachutePackChestStrap", Smooth(new[]
                {
                    new Vector3(-0.115f, 0.02f, chestStrapZ),
                    new Vector3(0f, 0.02f, chestStrapZ + 0.006f),
                    new Vector3(0.115f, 0.02f, chestStrapZ),
                }), Vector3.up),
                strap);
            Knob(front.transform, "Buckle", metal, new Vector3(0f, 0.02f, chestStrapZ + 0.01f), new Vector3(0.04f, 0.03f, 0.012f));
            Knob(root.transform, "PilotChuteHandle", handle, new Vector3(0.12f, -0.33f, -0.07f), Vector3.one * 0.035f);

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return root;
        }

        [MenuItem("Parachute/Build Pack Assets")]
        public static void BuildMenu()
        {
            Build();
            Debug.Log("[Parachute] Pack assets built.");
        }

        public static void Build()
        {
            CanopyBuilder.EnsureFolders();
            Texture2D fabric = CreatePackTexture();
            Material main = CanopyBuilder.CreateMaterial("PackMain", Olive, 0.1f, false, fabric);
            Material reserve = CanopyBuilder.CreateMaterial("PackReserve", Tan, 0.1f, false, fabric);
            Material strap = AssetDatabase.LoadAssetAtPath<Material>(CanopyBuilder.MaterialsDir + "/Strap.mat");
            if (strap == null) strap = CanopyBuilder.CreateMaterial("PackStrap", new Color(0.2f, 0.2f, 0.15f), 0.05f, false);
            Material metal = AssetDatabase.LoadAssetAtPath<Material>(CanopyBuilder.MaterialsDir + "/Link.mat");
            if (metal == null) metal = CanopyBuilder.CreateMaterial("PackMetal", new Color(0.22f, 0.22f, 0.21f), 0.55f, false);
            Material handle = CanopyBuilder.CreateMaterial("PackHandle", new Color(0.85f, 0.55f, 0.10f), 0.3f, false);

            GameObject root = CreatePack(main, reserve, strap, metal, handle);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
