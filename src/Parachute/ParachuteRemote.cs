using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Other players' canopies, sounds and packs, on this client. Nothing new is sent
/// over the network: a player's own client adds buffParachuteDeployed while their
/// canopy is open, and the game already passes buffs to every client near that
/// player (at once on add and remove, and again in its regular buff sync, which
/// also covers players who join or come into range later). Every client that has
/// this DLL watches for that buff and draws the canopy itself. The banking of
/// another player's canopy is estimated from how they move.
///
/// Ticked by the local player's ParachuteController, so it never runs on a
/// dedicated server.
/// </summary>
internal static class ParachuteRemote
{
    private const string DeployedBuff = "buffParachuteDeployed";
    /// <summary>How often the worn-pack backstop runs for other players, in seconds.</summary>
    private const float PackSyncInterval = 0.5f;

    private sealed class Remote
    {
        internal EntityPlayer Player;
        internal int EntityId;
        internal CanopyRig Rig;
        internal bool Open;
        internal bool Flutter;
        internal Vector3 LastPosition;
        internal float LastYaw;
        internal Vector3 Velocity;
        internal float YawRate;
        internal float Turn;
        internal bool Forward;
        internal bool Back;
    }

    private static readonly Dictionary<int, Remote> remotes = new Dictionary<int, Remote>();
    private static readonly HashSet<int> seen = new HashSet<int>();
    private static readonly List<int> gone = new List<int>();
    private static float nextPackSync;

    /// <summary>Per frame (Update): open, update and close other players' canopies.</summary>
    internal static void Tick(EntityPlayerLocal local)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.Players == null) return;
        ParachuteConfig config = ParachuteMod.Settings;

        bool packSync = Time.time >= nextPackSync;
        if (packSync) nextPackSync = Time.time + PackSyncInterval;

        seen.Clear();
        List<EntityPlayer> players = world.Players.list;
        for (int i = 0; i < players.Count; i++)
        {
            EntityPlayer player = players[i];
            if (player == null || player == local || player is EntityPlayerLocal) continue;
            try
            {
                if (packSync) ParachuteWorn.SyncRemote(player);
                TickPlayer(player, config);
                seen.Add(player.entityId);
            }
            catch (Exception ex)
            {
                if (config.Debug) Log.Warning("[Parachute] Remote player " + player.entityId + ": " + ex.Message);
            }
        }

        // Players who left, died or went out of range take their canopy with them.
        gone.Clear();
        foreach (KeyValuePair<int, Remote> pair in remotes)
            if (!seen.Contains(pair.Key)) gone.Add(pair.Key);
        foreach (int id in gone)
        {
            Remote remote = remotes[id];
            StopFlutter(remote);
            if (remote.Rig != null) remote.Rig.Destroy();
            remotes.Remove(id);
        }
    }

    private static void TickPlayer(EntityPlayer player, ParachuteConfig config)
    {
        bool deployed = !player.IsDead() && player.Buffs != null && player.Buffs.HasBuff(DeployedBuff)
            && player.AttachedToEntity == null;
        remotes.TryGetValue(player.entityId, out Remote remote);
        if (remote == null)
        {
            if (!deployed) return;
            remote = new Remote { Player = player, EntityId = player.entityId };
            ResetMotion(remote);
            remotes[player.entityId] = remote;
        }

        if (deployed && !remote.Open) OpenCanopy(remote, config);
        else if (!deployed && remote.Open) CloseCanopy(remote, config);
        if (remote.Open) EstimateInput(remote, config);
    }

    private static void OpenCanopy(Remote remote, ParachuteConfig config)
    {
        // Opened again before the last one finished collapsing.
        if (remote.Rig != null) remote.Rig.Destroy();
        remote.Open = true;
        ResetMotion(remote);
        remote.Rig = CanopyRig.Create(remote.Player, config);
        ParachuteAudio.PlayRemoteDeploy(remote.Player);
        if (config.PlayFlutterLoop)
        {
            ParachuteAudio.StartRemoteFlutter(remote.Player, config.FlutterPitch);
            remote.Flutter = true;
        }
        if (config.Debug) Log.Out("[Parachute] " + remote.Player.EntityName + "'s canopy opened.");
    }

    private static void CloseCanopy(Remote remote, ParachuteConfig config)
    {
        remote.Open = false;
        remote.Turn = 0f;
        remote.Forward = remote.Back = false;
        if (remote.Rig != null) remote.Rig.Stow();
        StopFlutter(remote);
        if (config.Debug) Log.Out("[Parachute] " + remote.Player.EntityName + "'s canopy stowed.");
    }

    private static void StopFlutter(Remote remote)
    {
        if (!remote.Flutter) return;
        remote.Flutter = false;
        ParachuteAudio.StopRemoteFlutter(remote.EntityId);
    }

    private static void ResetMotion(Remote remote)
    {
        remote.LastPosition = remote.Player.transform.position;
        remote.LastYaw = remote.Player.transform.eulerAngles.y;
        remote.Velocity = Vector3.zero;
        remote.YawRate = 0f;
    }

    /// <summary>
    /// Banks and pitches the canopy the way the owner's would, from the smoothed
    /// movement the game shows for that player, judged against this client's own
    /// speed settings: sliding sideways (or turning, in turn mode) rolls it, gliding
    /// faster than normal dives it and slower flares it.
    /// </summary>
    private static void EstimateInput(Remote remote, ParachuteConfig config)
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;
        Transform body = remote.Player.transform;
        Vector3 position = body.position;
        float yaw = body.eulerAngles.y;
        Vector3 moved = position - remote.LastPosition;
        float turned = Mathf.DeltaAngle(remote.LastYaw, yaw);
        remote.LastPosition = position;
        remote.LastYaw = yaw;
        // A teleport or an origin shift is not movement.
        if (moved.sqrMagnitude > 50f * 50f) return;

        float blend = Mathf.Clamp01(deltaTime * 4f);
        remote.Velocity = Vector3.Lerp(remote.Velocity, moved / deltaTime, blend);
        remote.YawRate = Mathf.Lerp(remote.YawRate, turned / deltaTime, blend);

        Vector3 forward = body.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f) return;
        forward.Normalize();
        Vector3 right = new Vector3(forward.z, 0f, -forward.x);
        float forwardSpeed = Vector3.Dot(remote.Velocity, forward);

        float turn = config.LeftRight == LeftRightMode.Strafe
            ? Vector3.Dot(remote.Velocity, right) / Mathf.Max(0.1f, config.StrafeSpeed)
            : remote.YawRate / Mathf.Max(1f, config.TurnRate);
        remote.Turn = Mathf.Clamp(turn, -1f, 1f);
        remote.Forward = forwardSpeed > (config.GlideSpeed + config.GlideSpeedForward) * 0.5f;
        remote.Back = forwardSpeed < (config.GlideSpeed + config.GlideSpeedBack) * 0.5f;
    }

    /// <summary>Per frame (LateUpdate): animate the rigs after the bodies have moved.</summary>
    internal static void LateTick()
    {
        if (remotes.Count == 0) return;
        ParachuteConfig config = ParachuteMod.Settings;
        gone.Clear();
        foreach (KeyValuePair<int, Remote> pair in remotes)
        {
            Remote remote = pair.Value;
            if (remote.Rig != null && !remote.Rig.Tick(config, remote.Turn, remote.Forward, remote.Back))
                remote.Rig = null;
            if (remote.Rig == null && !remote.Open) gone.Add(pair.Key);
        }
        foreach (int id in gone) remotes.Remove(id);
    }

    /// <summary>True while this game shows that other player under an open canopy (Patch_RemoteAvatarAnimation).</summary>
    internal static bool IsOpen(int entityId)
    {
        return remotes.TryGetValue(entityId, out Remote remote) && remote.Open;
    }

    /// <summary>For "chute status": which other players this client shows under a canopy.</summary>
    internal static string Describe()
    {
        var open = new List<string>();
        foreach (Remote remote in remotes.Values)
            if (remote.Open)
                open.Add((remote.Player != null ? remote.Player.EntityName : "?") + (remote.Rig == null ? " (no model)" : ""));
        return open.Count == 0 ? "none" : string.Join(", ", open.ToArray());
    }

    /// <summary>Removes every remote canopy and sound (leaving the world).</summary>
    internal static void Clear()
    {
        foreach (Remote remote in remotes.Values)
        {
            StopFlutter(remote);
            if (remote.Rig != null) remote.Rig.Destroy();
        }
        remotes.Clear();
        seen.Clear();
        gone.Clear();
        nextPackSync = 0f;
    }
}
