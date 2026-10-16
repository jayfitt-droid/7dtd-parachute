using System;
using UnityEngine;

/// <summary>
/// Lives on the local player entity. Owns input, the Jump press, the deploy /
/// stow decisions and the per-step speed targets that the Harmony patches read
/// back out of ParachuteState. Phase 1 is local-only.
/// </summary>
internal sealed class ParachuteController : MonoBehaviour
{
    internal static ParachuteController Instance;

    private EntityPlayerLocal player;
    private Vector3 lastPosition;
    private bool ropeWasAttached;
    // This controller put the camera in third person, so stowing puts it back. It
    // stays set after a stow until the switch back has actually happened.
    private bool switchedToThirdPerson;
    // The view is decided once per deploy, when the delay runs out, so a view the
    // player picks after that is never overridden.
    private bool viewDecided;
    // What the worn pack was last synced for; a change re-syncs it.
    private bool lastHasPack;
    private bool lastFirstPerson;
    private bool lastShowPack;

    internal static void Attach(EntityPlayerLocal entityPlayer)
    {
        if (entityPlayer == null) return;
        if (Instance != null && Instance.player != entityPlayer)
        {
            Destroy(Instance);
            Instance = null;
        }
        if (Instance == null)
        {
            Instance = entityPlayer.gameObject.AddComponent<ParachuteController>();
            Instance.player = entityPlayer;
        }
        ParachuteState.Player = entityPlayer;
        ParachuteState.Reset();
        Instance.lastPosition = entityPlayer.transform.position;
    }

    internal static void Detach()
    {
        if (Instance != null)
        {
            Destroy(Instance);
            Instance = null;
        }
        ParachuteVisual.Destroy();
        ParachuteWorn.Destroy();
        ParachuteAudio.Reset();
        ParachuteState.Reset();
    }

    internal static void ForceDeploy(EntityPlayerLocal entityPlayer)
    {
        Attach(entityPlayer);
        if (Instance == null) return;
        if (!ParachuteMod.PatchesOk)
        {
            Log.Out("[Parachute] Deploy disabled: a Harmony patch failed at startup (see log).");
            return;
        }
        if (!entityPlayer.Buffs.HasBuff("buffParachuteHarness"))
        {
            Log.Out("[Parachute] No parachute harness equipped; nothing to deploy.");
            return;
        }
        if (ParachuteState.Phase == ChutePhase.Stowed) Instance.Deploy();
    }

    internal static void ForceStow()
    {
        if (Instance != null && ParachuteState.IsActive) Instance.Stow("command");
    }

    private void Update()
    {
        if (GameManager.IsDedicatedServer) return;
        if (player == null)
        {
            player = ParachuteState.Player;
            if (player == null) return;
        }
        try
        {
            Tick();
        }
        catch (Exception ex)
        {
            Log.Warning("[Parachute] Update error: " + ex.Message);
        }
    }

    private void FixedUpdate()
    {
        if (GameManager.IsDedicatedServer || player == null) return;
        try
        {
            TickSpeedTargets();
        }
        catch (Exception ex)
        {
            Log.Warning("[Parachute] FixedUpdate error: " + ex.Message);
        }
    }

    private void LateUpdate()
    {
        if (GameManager.IsDedicatedServer || player == null) return;
        try
        {
            ParachuteVisual.Tick(ParachuteMod.Settings, ParachuteState.Phase,
                ParachuteState.LastTurn, ParachuteState.LastForward, ParachuteState.LastBack);
        }
        catch (Exception ex)
        {
            Log.Warning("[Parachute] LateUpdate error: " + ex.Message);
        }
    }

    private void Tick()
    {
        ParachuteConfig config = ParachuteMod.Settings;
        if (player.IsDead() || GameManager.Instance == null || GameManager.Instance.World == null)
        {
            if (ParachuteState.IsActive) Stow("gone");
            // The death camera and the respawn set the view themselves.
            if (player.IsDead()) switchedToThirdPerson = false;
            return;
        }

        Vector3 position = player.transform.position;
        if (ParachuteState.IsActive && (position - lastPosition).sqrMagnitude > 50f * 50f)
        {
            lastPosition = position;
            Stow("teleport");
            return;
        }
        lastPosition = position;
        SyncWornBackstop(config);

        if (ParachuteState.IsActive)
        {
            string reason = AutoStowReason();
            if (reason != null) Stow(reason);
        }

        // A switch back that had to wait for the player to stop aiming.
        if (!ParachuteState.IsActive && switchedToThirdPerson) RestoreView();

        // Keep buffParachuteDeployed present while active and through the landing grace.
        bool wantBuff = ParachuteState.IsActive || ParachuteState.InGrace;
        bool hasBuff = player.Buffs.HasBuff("buffParachuteDeployed");
        if (wantBuff && !hasBuff) AddDeployedBuff();
        else if (!wantBuff && hasBuff) player.Buffs.RemoveBuff("buffParachuteDeployed", -1, true);

        PlayerActionsLocal input = player.playerInput;
        bool airborne = !Grounded();
        bool jumpEnabled = config.DeployInput == DeployInput.Jump || config.DeployInput == DeployInput.Both;
        bool keyEnabled = (config.DeployInput == DeployInput.Key || config.DeployInput == DeployInput.Both)
            && config.DeployKeyCode != KeyCode.None;

        // Jump also lets go of a grappling rope. That press belongs to the rope, or
        // letting go would open the canopy at once. The rope detaches in the same
        // frame as the press, before or after this runs, so a rope seen on the
        // previous frame counts too.
        bool ropeNow = ParachuteRope.Attached;
        bool ropePress = ropeNow || ropeWasAttached;
        ropeWasAttached = ropeNow;

        if (jumpEnabled && input != null && input.Jump != null && input.Jump.WasPressed)
        {
            // One press in the air toggles the canopy. The press that starts a jump
            // is made on the ground, so it never counts.
            if (config.Debug)
                Log.Out("[Parachute] Jump press: airborne=" + airborne + " rope=" + ropePress);
            if (airborne && !ropePress) Toggle();
        }
        if (keyEnabled && Input.GetKeyDown(config.DeployKeyCode)) Toggle();

        if (ParachuteState.IsActive)
        {
            AutoThirdPerson(config);
            ParachuteFlight.ReadInput(input, out bool forward, out bool back, out float turn);
            ParachuteState.LastForward = forward;
            ParachuteState.LastBack = back;
            ParachuteState.LastTurn = turn;
            if (config.LeftRight == LeftRightMode.Turn && Mathf.Abs(turn) > 0.01f)
                ParachuteFlight.TurnLook(player, config.TurnRate * Time.deltaTime * turn);
        }
    }

    private void TickSpeedTargets()
    {
        ParachuteConfig config = ParachuteMod.Settings;
        if (ParachuteState.Phase == ChutePhase.Deploying)
        {
            float t = config.DeployTime > 0f ? (Time.time - ParachuteState.PhaseStartTime) / config.DeployTime : 1f;
            float eased = ParachuteFlight.EaseOut(t);
            ParachuteFlight.ReadInput(player.playerInput, out bool forward, out bool back, out float turn);
            ParachuteFlight.Targets(config, forward, back, out float glideTarget, out float fallTarget);
            ParachuteState.CurrentFallSpeed = Mathf.Lerp(ParachuteState.InitialFallSpeed, fallTarget, eased);
            ParachuteState.CurrentGlideSpeed = Mathf.Lerp(0f, glideTarget, eased);
            ParachuteState.CurrentStrafeSpeed = ParachuteFlight.Approach(ParachuteState.CurrentStrafeSpeed,
                ParachuteFlight.StrafeTarget(config, turn) * eased, config.ControlResponse, Time.fixedDeltaTime);
            if (t >= 1f) ParachuteState.Phase = ChutePhase.Open;
        }
        else if (ParachuteState.Phase == ChutePhase.Open)
        {
            ParachuteFlight.ReadInput(player.playerInput, out bool forward, out bool back, out float turn);
            ParachuteFlight.Targets(config, forward, back, out float glideTarget, out float fallTarget);
            float deltaTime = Time.fixedDeltaTime;
            ParachuteState.CurrentStrafeSpeed = ParachuteFlight.Approach(ParachuteState.CurrentStrafeSpeed,
                ParachuteFlight.StrafeTarget(config, turn), config.ControlResponse, deltaTime);
            ParachuteState.CurrentFallSpeed = ParachuteFlight.Approach(ParachuteState.CurrentFallSpeed, fallTarget, config.ControlResponse, deltaTime);
            ParachuteState.CurrentGlideSpeed = ParachuteFlight.Approach(ParachuteState.CurrentGlideSpeed, glideTarget, config.ControlResponse, deltaTime);
        }
        else if (ParachuteState.Phase == ChutePhase.Stowing)
        {
            if (Time.time - ParachuteState.PhaseStartTime >= config.StowAnimTime)
            {
                ParachuteState.Phase = ChutePhase.Stowed;
                ParachuteState.RedeployReadyTime = Time.time + config.RedeployCooldownSeconds;
                ParachuteState.CurrentGlideSpeed = 0f;
                ParachuteState.CurrentFallSpeed = 0f;
                ParachuteState.CurrentStrafeSpeed = 0f;
            }
        }
    }

    private void Toggle()
    {
        if (ParachuteState.Phase == ChutePhase.Stowed)
        {
            if (Time.time < ParachuteState.RedeployReadyTime) return;
            // Without a harness every Jump press in the air lands here; say nothing.
            if (!player.Buffs.HasBuff("buffParachuteHarness"))
            {
                if (ParachuteMod.Settings.Debug) Log.Out("[Parachute] Deploy refused: no harness (silent)");
                return;
            }
            string blocked = DeployBlockReason();
            if (blocked == null)
            {
                Deploy();
                return;
            }
            Log.Out("[Parachute] Deploy refused: " + blocked);
            GameManager.ShowTooltip(player, "Parachute: " + blocked);
        }
        else if (ParachuteState.IsActive && ParachuteMod.Settings.AllowCutaway)
        {
            float lockoutEnds = ParachuteState.DeployedAt + ParachuteMod.Settings.CutawayLockoutSeconds;
            if (Time.time < lockoutEnds)
            {
                if (ParachuteMod.Settings.Debug)
                    Log.Out("[Parachute] Cutaway ignored: lockout (" + (lockoutEnds - Time.time).ToString("F2") + "s left)");
                return;
            }
            Stow("cutaway");
        }
    }

    /// <summary>Null when a deploy is allowed, otherwise why it is not.</summary>
    private string DeployBlockReason()
    {
        ParachuteConfig config = ParachuteMod.Settings;
        if (!ParachuteMod.PatchesOk) return "a Harmony patch failed at startup (see log)";
        if (!player.Buffs.HasBuff("buffParachuteHarness")) return "no parachute harness on equipped chest armor";
        if (Grounded()) return "on the ground";
        if (!HasClearAirBelow(config.MinDeployHeight))
            return "too low (needs " + config.MinDeployHeight.ToString("F1") + " m of clear air below)";
        if (player.IsInWater() || player.IsSwimming()) return "in water";
        if (player.isLadderAttached) return "on a ladder";
        if (player.AttachedToEntity != null) return "in a vehicle";
        if (player.IsFlyMode.Value || player.IsGodMode.Value) return "fly or god mode is on";
        if (player.JetpackWearing) return "wearing a jetpack";
        if (UiBlocksInput()) return "a window is open";
        if (Time.time < ParachuteState.RedeployReadyTime) return "redeploy cooldown";
        return null;
    }

    private void Deploy()
    {
        ParachuteConfig config = ParachuteMod.Settings;
        float descent = 0f;
        if (player.vp_FPController != null)
            descent = Mathf.Max(0f, -player.vp_FPController.m_FallSpeed * ParachuteFlight.VpStepsPerSecondFactor);

        ParachuteState.Phase = ChutePhase.Deploying;
        ParachuteState.PhaseStartTime = Time.time;
        ParachuteState.DeployedAt = Time.time;
        viewDecided = false;
        ParachuteState.InitialFallSpeed = descent;
        ParachuteState.CurrentFallSpeed = descent;
        ParachuteState.CurrentGlideSpeed = 0f;
        ParachuteState.CurrentStrafeSpeed = 0f;

        SyncFallAnimation();
        ParachuteVisual.Deploy(player, config);
        ParachuteAudio.PlayDeploy(player);
        if (config.PlayFlutterLoop) ParachuteAudio.StartFlutter(player, config.FlutterPitch);
        if (config.Debug) Log.Out("[Parachute] Deployed at " + descent.ToString("F1") + " m/s descent.");
    }

    internal void Stow(string reason)
    {
        if (!ParachuteState.IsActive) return;
        ParachuteConfig config = ParachuteMod.Settings;
        if (reason == "landed")
            ParachuteState.LandingGraceUntil = Time.time + config.LandingGraceSeconds;

        ParachuteState.Phase = ChutePhase.Stowing;
        ParachuteState.PhaseStartTime = Time.time;
        ParachuteState.LastStowReason = reason;
        SyncFallAnimation();
        RestoreView();
        ParachuteVisual.Stow(config);
        ParachuteAudio.StopFlutter(player);
        if (config.Debug) Log.Out("[Parachute] Stowing: " + reason);
    }

    /// <summary>
    /// After third_person_after_ms under the canopy, switch to third person the way
    /// the game does when a player gets into a vehicle (as in the GliderSuit mod).
    /// Nothing happens if the player is already in third person or the world
    /// restricts the camera. While the player is aiming it waits until they stop.
    /// </summary>
    private void AutoThirdPerson(ParachuteConfig config)
    {
        if (viewDecided || !config.AutoThirdPerson) return;
        if (Time.time - ParachuteState.DeployedAt < config.ThirdPersonAfterSeconds) return;
        if (ViewSwitchBlocked()) return;
        viewDecided = true;
        if (!player.bFirstPersonView) return;
        if (player.vp_FPCamera == null || player.vp_FPCamera.Locked3rdPerson) return;
        if (GameStats.GetInt(EnumGameStats.CameraRestrictionMode) != 0) return;
        player.SetFirstPersonView(false, true);
        switchedToThirdPerson = true;
        if (config.Debug) Log.Out("[Parachute] View: third person under the canopy.");
    }

    /// <summary>
    /// Back to the view the player last chose with the view key (the game's own
    /// preference, as when leaving a vehicle). Not while dead: the death camera and
    /// the respawn set the view themselves. While the player is aiming it waits:
    /// Tick calls this again every frame until the switch happens.
    /// </summary>
    /// <summary>
    /// The SDCS postfixes cover most cases; this catches installing or removing
    /// the mod, switching views and "chute reload" toggling show_pack, in case
    /// none of them rebuilds the rig. Also re-attaches the pack if a rebuild took
    /// it away. Ported from the GliderSuit mod.
    /// </summary>
    private void SyncWornBackstop(ParachuteConfig config)
    {
        bool hasPack = ParachuteEquip.HasParachute(player);
        bool firstPerson = player.bFirstPersonView;
        bool changed = hasPack != lastHasPack || firstPerson != lastFirstPerson || config.ShowPack != lastShowPack;
        lastHasPack = hasPack;
        lastFirstPerson = firstPerson;
        lastShowPack = config.ShowPack;

        bool missing = config.ShowPack && hasPack && !firstPerson && !ParachuteWorn.WorldPresent;
        if (changed || missing)
        {
            EModelSDCS model = player.emodel as EModelSDCS;
            if (model != null) ParachuteWorn.Sync(player, model.boneCatalog, WornKind.World);
        }
        ParachuteWorn.Tick(player);
    }

    private bool restoreWaitLogged;

    /// <summary>
    /// Changing the view while aiming swaps the weapon between its first- and
    /// third-person models mid-zoom and leaves the zoom stuck on after the player
    /// lets go. The game avoids it too: its view key does nothing while an item
    /// action runs, and a vehicle clears AimingGun before its camera takes over.
    /// </summary>
    private bool ViewSwitchBlocked()
    {
        if (player.AimingGun) return true;
        return player.inventory != null && player.inventory.IsHoldingItemActionRunning();
    }

    private void RestoreView()
    {
        if (!switchedToThirdPerson) return;
        if (player.IsDead())
        {
            switchedToThirdPerson = false;
            return;
        }
        if (ViewSwitchBlocked())
        {
            if (ParachuteMod.Settings.Debug && !restoreWaitLogged)
                Log.Out("[Parachute] View: switch back waits until you stop aiming.");
            restoreWaitLogged = true;
            return;
        }
        switchedToThirdPerson = false;
        restoreWaitLogged = false;
        player.SwitchToPreferredCameraMode(true);
        if (ParachuteMod.Settings.Debug)
            Log.Out("[Parachute] View: back to " + (player.bFirstPersonView ? "first" : "third") + " person.");
    }

    /// <summary>
    /// The game only sends "in the air" to the animator when it changes, so push
    /// it again whenever the canopy opens or closes. Patch_AvatarAnimation turns
    /// it into "not falling" while the canopy is active.
    /// </summary>
    private void SyncFallAnimation()
    {
        if (player.emodel != null && player.emodel.avatarController != null)
            player.emodel.avatarController.SetInAir(player.inAir);
    }

    private string AutoStowReason()
    {
        if (!player.Buffs.HasBuff("buffParachuteHarness")) return "mod removed";
        if (player.IsDead()) return "death";
        if (player.IsInWater() || player.IsSwimming()) return "water";
        if (player.isLadderAttached) return "ladder";
        if (player.AttachedToEntity != null) return "attached";
        if (player.IsFlyMode.Value || player.IsGodMode.Value) return "fly/god";
        if (Grounded()) return "landed";
        return null;
    }

    private void AddDeployedBuff()
    {
        EntityBuffs.BuffStatus status = player.Buffs.AddBuff("buffParachuteDeployed", -1, true);
        if (status != EntityBuffs.BuffStatus.Added)
            Log.Warning("[Parachute] Could not add buffParachuteDeployed: " + status);
    }

    private bool Grounded()
    {
        if (player.onGround) return true;
        return player.vp_FPController != null && player.vp_FPController.Grounded;
    }

    private bool HasClearAirBelow(float meters)
    {
        if (meters <= 0f) return true;
        Vector3 origin = player.transform.position + Vector3.up * 0.1f;
        return !Physics.Raycast(origin, Vector3.down, out RaycastHit _, meters, GroundMask);
    }

    private bool UiBlocksInput()
    {
        try
        {
            LocalPlayerUI ui = player.PlayerUI;
            GUIWindowManager windows = ui != null ? ui.windowManager : null;
            if (windows != null && (windows.IsModalWindowOpen() || windows.IsCursorWindowOpen() || windows.IsInputActive()))
                return true;
            return GameManager.Instance != null && GameManager.Instance.isAnyCursorWindowOpen(ui);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void OnGUI()
    {
        if (!ParachuteMod.Settings.Debug || ParachuteState.Player != player) return;
        try
        {
            float ground = 0f;
            if (Physics.Raycast(player.transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 500f, GroundMask))
                ground = hit.distance;
            string text = "[Parachute] " + ParachuteState.Phase +
                "  descent " + ParachuteState.CurrentFallSpeed.ToString("F1") + " m/s" +
                "  glide " + ParachuteState.CurrentGlideSpeed.ToString("F1") + " m/s" +
                "  strafe " + ParachuteState.CurrentStrafeSpeed.ToString("F1") + " m/s" +
                "  ground " + ground.ToString("F1") + " m";
            GUI.Label(new Rect(10f, 10f, 700f, 24f), text);
        }
        catch (Exception) { }
    }

    /// <summary>The same layer mask vp_FPController uses for its ground sphere cast.</summary>
    private const int GroundMask = 1084850184;
}
