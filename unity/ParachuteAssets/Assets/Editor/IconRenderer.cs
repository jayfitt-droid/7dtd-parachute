using System.IO;
using UnityEditor;
using UnityEngine;

namespace ParachuteEditor
{
    /// <summary>
    /// Renders the inventory icon for modArmorParachute the way the game's own
    /// item icons are made: a lit 3D object on a transparent background, seen
    /// from a three-quarter angle. The object is the mod's real canopy with its
    /// lines gathered into a small harness pack. Needs a graphics device, so run
    /// it without -nographics:
    ///   Unity.exe -batchmode -quit -projectPath unity/ParachuteAssets
    ///             -executeMethod ParachuteEditor.IconRenderer.Render
    /// Writes a 640x640 master to %TEMP%\ParachutePreview\Icon_master.png;
    /// tools/make_icon.py trims and shrinks it to the 160x160 icon in the mod.
    /// </summary>
    public static class IconRenderer
    {
        private const int Size = 640;

        public static void Render()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanopyBuilder.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Parachute] Icon: prefab missing, build the canopy first.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var scene = new GameObject("IconScene");
            GameObject canopy = Object.Instantiate(prefab, scene.transform, false);

            // Gather the lines at the top corners of the pack and thicken them so
            // they survive being shrunk to icon size.
            var packTop = new Vector3(0f, -1.25f, 0f);
            foreach (LineRenderer line in canopy.GetComponentsInChildren<LineRenderer>())
            {
                // Half the rows are enough at this size; the full set turns to mush.
                if (line.name.EndsWith("_1") || line.name.EndsWith("_3"))
                {
                    line.enabled = false;
                    continue;
                }
                Vector3 anchor = line.GetPosition(0);
                line.SetPosition(1, packTop + new Vector3(anchor.x < 0f ? -0.45f : 0.45f, 0.25f, anchor.z < 0f ? -0.1f : 0.1f));
                line.widthMultiplier = 4.5f;
            }
            BuildPack(scene.transform, packTop);

            Light key = AddLight(scene.transform, "Key", new Vector3(40f, 210f, 0f), 1.0f, new Color(1f, 0.97f, 0.9f));
            AddLight(scene.transform, "Fill", new Vector3(10f, 140f, 0f), 0.3f, new Color(0.75f, 0.82f, 1f));
            AddLight(scene.transform, "Under", new Vector3(-50f, 180f, 0f), 0.35f, new Color(0.9f, 0.88f, 0.8f));
            key.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.34f, 0.36f);

            var cameraObject = new GameObject("IconCamera");
            cameraObject.transform.SetParent(scene.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 3.9f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            var target = new Vector3(0f, -0.9f, 0f);
            camera.transform.position = target + new Vector3(-8f, 6.5f, 14f);
            camera.transform.LookAt(target);

            var renderTexture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;

            string directory = Path.Combine(Path.GetTempPath(), "ParachutePreview");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Icon_master.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log("[Parachute] Icon master written to " + path);

            Object.DestroyImmediate(image);
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(scene);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// A soft olive harness pack built from rounded shapes: the packed
        /// bundle, two webbing bands cinched around it with a buckle each, and
        /// the two risers the lines gather into.
        /// </summary>
        private static void BuildPack(Transform parent, Vector3 top)
        {
            Material olive = Flat(new Color(0.27f, 0.28f, 0.17f), 0.12f);
            Material strap = Flat(new Color(0.42f, 0.36f, 0.24f), 0.08f);
            Material dark = Flat(new Color(0.10f, 0.09f, 0.08f), 0.05f);
            Material metal = Flat(new Color(0.55f, 0.55f, 0.53f), 0.65f);

            Vector3 center = top + new Vector3(0f, -1.0f, 0f);
            Shape(parent, PrimitiveType.Sphere, olive, center, new Vector3(2.6f, 2.1f, 1.6f), Vector3.zero);
            foreach (float y in new[] { -0.38f, 0.38f })
            {
                Shape(parent, PrimitiveType.Sphere, strap, center + new Vector3(0f, y, 0f), new Vector3(2.5f, 0.28f, 1.56f), Vector3.zero);
                Shape(parent, PrimitiveType.Cube, metal, center + new Vector3(0.25f, y, 0.74f), new Vector3(0.36f, 0.3f, 0.06f), Vector3.zero);
            }
            foreach (float x in new[] { -0.45f, 0.45f })
                Shape(parent, PrimitiveType.Capsule, dark, top + new Vector3(x, 0.02f, 0f), new Vector3(0.24f, 0.36f, 0.18f), new Vector3(0f, 0f, x > 0f ? -8f : 8f));
        }

        private static void Shape(Transform parent, PrimitiveType type, Material material, Vector3 position, Vector3 size, Vector3 euler)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = size;
            shape.transform.localRotation = Quaternion.Euler(euler);
            shape.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material Flat(Color color, float smoothness)
        {
            var material = new Material(Shader.Find("Standard")) { color = color };
            material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        private static Light AddLight(Transform parent, string name, Vector3 euler, float intensity, Color color)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(euler);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            return light;
        }
    }
}
