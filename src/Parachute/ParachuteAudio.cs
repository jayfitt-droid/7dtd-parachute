using UnityEngine;

/// <summary>
/// Deploy one-shot and the vanilla in-flight flutter loop. The custom clip is
/// loaded from the bundle; if it is missing the vanilla Parachute_Opens sound
/// plays instead, so there is always an audible deploy.
/// </summary>
internal static class ParachuteAudio
{
    private static AudioSource deploySource;
    private static bool flutterPlaying;

    internal static void PlayDeploy(EntityPlayerLocal player)
    {
        if (player == null) return;
        AudioClip clip = ParachuteAssets.DeployClip;
        if (clip != null)
        {
            if (deploySource == null)
            {
                var go = new GameObject("ParachuteDeployAudio");
                go.transform.SetParent(player.transform, false);
                deploySource = go.AddComponent<AudioSource>();
                deploySource.spatialBlend = 0f;
                deploySource.playOnAwake = false;
            }
            deploySource.Stop();
            deploySource.clip = clip;
            deploySource.Play();
        }
        else
        {
            Audio.Manager.PlayInsidePlayerHead("Parachute_Opens", player.entityId, 0f, false, false);
        }
    }

    internal static void StartFlutter(EntityPlayerLocal player, float pitch)
    {
        if (player == null || flutterPlaying) return;
        flutterPlaying = true;
        Audio.Manager.PlayInsidePlayerHead("Parachute_Flutter_Lp", player.entityId, 0f, true, false);

        // The sound XML cannot pitch a sound below 0.85, so retune the playing
        // source directly. A lower pitch is deeper and flaps more slowly.
        try
        {
            if (Audio.Manager.loopingOnEntity.TryGetValue(player.entityId, out var loops) &&
                loops.TryGetValue("Parachute_Flutter_Lp", out var loop) && loop.near != null)
            {
                AudioSource source = loop.near.GetComponent<AudioSource>();
                if (source != null) source.pitch = pitch;
            }
        }
        catch (System.Exception ex)
        {
            Log.Warning("[Parachute] Could not set the flutter pitch: " + ex.Message);
        }
    }

    internal static void StopFlutter(EntityPlayerLocal player)
    {
        if (!flutterPlaying) return;
        flutterPlaying = false;
        if (player != null) Audio.Manager.Stop(player.entityId, "Parachute_Flutter_Lp");
    }

    internal static void Reset()
    {
        flutterPlaying = false;
        if (deploySource != null) Object.Destroy(deploySource.gameObject);
        deploySource = null;
    }
}
