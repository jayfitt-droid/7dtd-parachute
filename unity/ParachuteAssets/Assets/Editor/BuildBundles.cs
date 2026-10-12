using System.IO;
using UnityEditor;
using UnityEngine;

namespace ParachuteEditor
{
    /// <summary>
    /// Builds the canopy assets and packs them into the Windows asset bundle
    /// that ships in the mod as Resources/Parachute.unity3d. The asset names in
    /// the bundle must stay ParachuteCanopy and ParachuteDeploy.
    ///
    /// Headless build:
    ///   Unity.exe -batchmode -nographics -quit -projectPath unity/ParachuteAssets
    ///             -executeMethod ParachuteEditor.BuildBundles.Build
    /// </summary>
    public static class BuildBundles
    {
        private const string BundleName = "parachute";
        private const string ShippedName = "Parachute.unity3d";

        [MenuItem("Parachute/Build Asset Bundle")]
        public static void BuildMenu() { Build(); }

        public static void Build()
        {
            CanopyBuilder.Build();

            string outputDir = Path.Combine(Path.GetTempPath(), "ParachuteBundle");
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            Directory.CreateDirectory(outputDir);

            var build = new AssetBundleBuild
            {
                assetBundleName = BundleName,
                assetNames = new[] { CanopyBuilder.PrefabPath, CanopyBuilder.AudioPath },
            };

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                outputDir, new[] { build },
                BuildAssetBundleOptions.ChunkBasedCompression,
                BuildTarget.StandaloneWindows64);

            if (manifest == null)
            {
                Debug.LogError("[Parachute] Asset bundle build failed.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            string built = Path.Combine(outputDir, BundleName);
            string project = Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
            string destinationDir = Path.GetFullPath(Path.Combine(project, "..", "..", "Mod", "Parachute", "Resources"));
            Directory.CreateDirectory(destinationDir);
            string destination = Path.Combine(destinationDir, ShippedName);
            File.Copy(built, destination, true);
            Debug.Log("[Parachute] Bundle written to " + destination);

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Loads the shipped bundle back and reports its assets. Used to confirm
        /// the names DataLoader will ask for are the ones packed inside.
        /// </summary>
        public static void Verify()
        {
            string project = Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
            string path = Path.GetFullPath(Path.Combine(project, "..", "..", "Mod", "Parachute", "Resources", ShippedName));
            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                Debug.LogError("[Parachute] Verify: could not load " + path);
                EditorApplication.Exit(1);
                return;
            }

            foreach (string assetName in bundle.GetAllAssetNames())
                Debug.Log("[Parachute] Verify asset: " + assetName);
            GameObject prefab = bundle.LoadAsset<GameObject>("ParachuteCanopy");
            AudioClip clip = bundle.LoadAsset<AudioClip>("ParachuteDeploy");
            Debug.Log("[Parachute] Verify prefab=" + (prefab != null) + " clip=" + (clip != null));
            bundle.Unload(true);
            EditorApplication.Exit(0);
        }
    }
}
