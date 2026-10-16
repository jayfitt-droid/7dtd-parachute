using System;
using UnityEngine;

/// <summary>
/// Art and audio from the mod's asset bundle (built by the Unity project in
/// unity/ParachuteAssets). Loaded through the game's DataLoader so the bundle
/// is cached alongside the game's own. Everything degrades gracefully when the
/// bundle is missing: the canopy stays invisible and the vanilla open sound is
/// played instead.
/// </summary>
internal static class ParachuteAssets
{
    private const string BundleUri = "#@modfolder(Parachute):Resources/Parachute.unity3d?";
    internal const string CanopyName = "ParachuteCanopy";
    internal const string PackName = "ParachutePack";
    internal const string DeployClipName = "ParachuteDeploy";

    private static bool loaded;
    private static bool warnedMissing;
    private static GameObject canopyPrefab;
    private static GameObject packPrefab;
    private static AudioClip deployClip;

    internal static GameObject CanopyPrefab
    {
        get { Load(); return canopyPrefab; }
    }

    internal static GameObject PackPrefab
    {
        get { Load(); return packPrefab; }
    }

    internal static AudioClip DeployClip
    {
        get { Load(); return deployClip; }
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            canopyPrefab = DataLoader.LoadAsset<GameObject>(BundleUri + CanopyName, false);
        }
        catch (Exception ex)
        {
            Log.Warning("[Parachute] Canopy model failed to load: " + ex.Message);
        }
        try
        {
            packPrefab = DataLoader.LoadAsset<GameObject>(BundleUri + PackName, false);
        }
        catch (Exception ex)
        {
            Log.Warning("[Parachute] Pack model failed to load: " + ex.Message);
        }
        try
        {
            deployClip = DataLoader.LoadAsset<AudioClip>(BundleUri + DeployClipName, false);
        }
        catch (Exception ex)
        {
            Log.Warning("[Parachute] Deploy sound failed to load: " + ex.Message);
        }

        if (canopyPrefab != null || packPrefab != null || deployClip != null)
            Log.Out("[Parachute] Asset bundle: canopy " + (canopyPrefab != null ? "loaded" : "missing") +
                ", pack " + (packPrefab != null ? "loaded" : "missing") +
                ", deploy sound " + (deployClip != null ? "loaded" : "missing") + ".");
        else if (!warnedMissing)
        {
            warnedMissing = true;
            Log.Warning("[Parachute] Asset bundle missing; the canopy is invisible and the vanilla open sound is used.");
        }
    }

    internal static void Clear()
    {
        loaded = false;
        canopyPrefab = null;
        packPrefab = null;
        deployClip = null;
    }
}
