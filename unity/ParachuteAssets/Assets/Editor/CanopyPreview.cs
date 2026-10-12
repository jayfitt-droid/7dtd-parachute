using System.IO;
using UnityEditor;
using UnityEngine;

namespace ParachuteEditor
{
    /// <summary>
    /// Renders the canopy prefab to PNG files so its look can be checked without
    /// starting the game. Needs a graphics device, so run it without -nographics:
    ///   Unity.exe -batchmode -quit -projectPath unity/ParachuteAssets
    ///             -executeMethod ParachuteEditor.CanopyPreview.Render
    /// The images go to %TEMP%\ParachutePreview\CanopyPreview_*.png.
    /// </summary>
    public static class CanopyPreview
    {
        public static void Render()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanopyBuilder.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Parachute] Preview: prefab missing, build the canopy first.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            GameObject canopy = Object.Instantiate(prefab);
            var lightObject = new GameObject("PreviewSun");
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.7f);

            var cameraObject = new GameObject("PreviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.62f, 0.85f);
            camera.fieldOfView = 50f;

            // Canopy root is 3.2 m above the feet, so the feet are at y = -3.2.
            Shoot(camera, "front_above", new Vector3(5f, 3.5f, 8f), new Vector3(0f, -0.2f, 0f));
            Shoot(camera, "behind_below", new Vector3(-1.5f, -3.5f, -5.5f), new Vector3(0f, 0.2f, 0f));
            Shoot(camera, "side", new Vector3(9f, 0.3f, 0f), new Vector3(0f, -0.3f, 0f));

            Object.DestroyImmediate(canopy);
            Object.DestroyImmediate(lightObject);
            Object.DestroyImmediate(cameraObject);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void Shoot(Camera camera, string name, Vector3 position, Vector3 target)
        {
            const int width = 1280, height = 720;
            camera.transform.position = position;
            camera.transform.LookAt(target);
            var renderTexture = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;

            string directory = Path.Combine(Path.GetTempPath(), "ParachutePreview");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "CanopyPreview_" + name + ".png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log("[Parachute] Preview written to " + path);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(renderTexture);
        }
    }
}
