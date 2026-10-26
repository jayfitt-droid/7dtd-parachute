using UnityEngine;

/// <summary>
/// Deploy one-shot and the vanilla in-flight flutter loop. The custom clip is
/// loaded from the bundle; if it is missing the vanilla Parachute_Opens sound
/// plays instead, so there is always an audible deploy.
/// </summary>
internal static class ParachuteAudio
{
    private const string FlutterLoop = "Parachute_Flutter_Lp";
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
        Audio.Manager.PlayInsidePlayerHead(FlutterLoop, player.entityId, 0f, true, false);
        SetLoopPitch(player.entityId, pitch);
    }

    /// <summary>
    /// The sound XML cannot pitch a sound below 0.85, so retune the playing
    /// source directly. A lower pitch is deeper and flaps more slowly.
    /// </summary>
    private static void SetLoopPitch(int entityId, float pitch)
    {
        try
        {
            if (Audio.Manager.loopingOnEntity.TryGetValue(entityId, out var loops) &&
                loops.TryGetValue(FlutterLoop, out var loop) && loop.near != null)
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

    /// <summary>
    /// Another player's deploy, heard from where they are. Each client plays it
    /// for itself when it sees that player's canopy open, so nothing is sent over
    /// the network. The source removes itself when the clip ends.
    /// </summary>
    internal static void PlayRemoteDeploy(EntityAlive other)
    {
        if (other == null) return;
        AudioClip clip = ParachuteAssets.DeployClip;
        if (clip == null)
        {
            Audio.Manager.Play(other, "Parachute_Opens", 1f, false);
            return;
        }
        var go = new GameObject("ParachuteDeployAudio");
        go.transform.SetParent(other.transform, false);
        go.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        AudioSource source = go.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 3f;
        source.maxDistance = 80f;
        source.dopplerLevel = 0f;
        source.playOnAwake = false;
        source.clip = clip;
        source.Play();
        Object.Destroy(go, clip.length + 0.5f);
    }

    /// <summary>The flutter loop on another player, positioned on them.</summary>
    internal static void StartRemoteFlutter(EntityAlive other, float pitch)
    {
        if (other == null) return;
        Audio.Manager.Play(other, FlutterLoop, 1f, false);
        SetLoopPitch(other.entityId, pitch);
    }

    internal static void StopRemoteFlutter(int entityId)
    {
        try
        {
            Audio.Manager.Stop(entityId, FlutterLoop);
        }
        catch (System.Exception)
        {
            // The entity may already be gone, taking its sounds with it.
        }
    }

    internal static void StopFlutter(EntityPlayerLocal player)
    {
        if (!flutterPlaying) return;
        flutterPlaying = false;
        if (player != null) Audio.Manager.Stop(player.entityId, FlutterLoop);
    }

    internal static void Reset()
    {
        flutterPlaying = false;
        if (deploySource != null) Object.Destroy(deploySource.gameObject);
        deploySource = null;
    }
}
