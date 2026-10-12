# Parachute Mod for 7 Days to Die v3.3 — Implementation Plan

> **Audience:** any engineer or AI model picking this up cold. Read §0–§3 first and skip nothing in §3 ("Verified engine facts"). Every claim tagged **[VERIFIED]** was read directly from the v3.3 decompiled source or game XML. **[VERIFY]** means "plausible, confirm before relying on it". Work the phases in §8 in order; each one has acceptance criteria.
>
> **Status:** planning only. No code has been written.
> **Author/owner:** Jason. Plan date: 2026-10-12.

---

## 0. TL;DR

A chest-armor mod item, **`modArmorParachute`**. With it installed in the chest piece ("outfit"), a player in the air **double-taps Jump** to deploy a steerable canopy:

- The canopy brakes the fall to a configurable **4 m/s** descent.
- **Forward** speeds up slightly, **Back** flares (slower and shallower), **Left/Right** turn.
- There is **no fall damage** on touchdown.

The mod also ships:

- a tier-2 **schematic** in the vanilla `schematicsModsAndGeneralT2` loot group
- a workbench recipe, an inventory icon and localized text
- a Unity AssetBundle with the canopy model and a custom deploy sound
- a JSON config
- `chute` console commands for testing

It is built as a C# DLL plus Harmony patches, following the same structure as Jason's existing `Mods/GrapplingHook` mod. **Phase 1 is single-player/host only.** **Phase 2** adds full multiplayer: remote players see the canopy, and dedicated servers are supported.

---

## 1. Requirements (from the user) and decisions made

| # | Requirement / decision | Source |
|---|---|---|
| R1 | Armor modification placed in a mod slot of the player's outfit | user |
| R2 | **Chest armor only** (`installable_tags="armorChest"`). In v3.3 the "Outfit" items *are* the chest pieces (`EquipSlot=Chest`, tag `armorChest`) | user + [VERIFIED] items.xml |
| R3 | Deploy only while off the ground and only with the mod equipped | user |
| R4 | Deploy input = **double-tap Jump while airborne** (replaces the original "F", which conflicts with the vanilla flashlight toggle). The input is defined in the config file | user |
| R5 | Default descent **4 m/s**, set in the config | user |
| R6 | Forward = speed up slightly. Back = slow down / glide. Left/Right = **turn** (yaw), not strafe | user |
| R7 | No fall damage when touching down with the canopy deployed | user |
| R8 | Crafting recipe, inventory icon, description, schematic | user |
| R9 | Schematic is **tier 2 = loot tier 2** (`schematicsModsAndGeneralT2`) | user |
| R10 | Assets: canopy model and inventory icon, as a **Unity AssetBundle** | user |
| R11 | A custom deploy sound effect is created | user |
| R12 | Console commands spawn an outfit, the schematic and the parachute mod into the player's inventory | user |
| R13 | MP: **v1 = single-player/host only. Later = full MP + dedicated** | user |
| R14 | `plan.md` lives in the current working folder | user |

### Defaults chosen by the planner (confirm or change; see §12)
- **Reusable:** the mod is not consumed and has no durability or repack time.
- **Cutaway:** double-tapping Jump again while deployed stows the canopy (config `allow_cutaway`, default `true`).
- **Minimum deploy height:** at least 3 m of clear air below the player (config). This stops a ground hop plus a double-tap from opening the chute at head height.
- **No quality tiers:** like Impact Bracing, the mod has no quality levels, and steering stats come only from the config.
- **Auto-stow** on landing, entering water, grabbing a ladder, mounting a vehicle, death, teleport/respawn, god or fly mode, or losing the mod/armor.

---

## 2. Environment & paths

| Thing | Location / value |
|---|---|
| Source repo root (this plan lives here) | `C:\Users\Jason\Projects\7d2d\Parachute` |
| Game install (v3.3) | `C:\7D2D\Custom\v3.3` |
| Managed DLLs to reference | `C:\7D2D\Custom\v3.3\7DaysToDie_Data\Managed\` (`Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll`, `UnityEngine.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.PhysicsModule.dll`, `UnityEngine.AudioModule.dll`, `UnityEngine.AssetBundleModule.dll`, `UnityEngine.InputLegacyModule.dll`, `LogLibrary.dll`, `Newtonsoft.Json.dll`, `InControl.dll`) |
| Harmony | `C:\7D2D\Custom\v3.3\Mods\0_TFP_Harmony\0Harmony.dll` (TFP's HarmonyX wrapper ships with the game) |
| Decompiled source (dotPeek) | `D:\dotPeek Test\Assembly-CSharp\` (flat `*.cs` per type, plus subfolders such as `Audio\`) |
| Vanilla config XML | `C:\7D2D\Custom\v3.3\Data\Config\` |
| Unity version used by the game | **2022.3.62f2** [VERIFIED from the bundle header of `data.unity3d` and `GrapplingHook.unity3d`] |
| Target framework | **.NET Framework 4.8** (matches GrapplingHook.dll [VERIFIED]) |
| Reference mod (same author) | `C:\7D2D\Custom\v3.3\Mods\GrapplingHook\`: copy its conventions for the JSON-with-comments config, `IModApi` + `Harmony.PatchAll`, `ConsoleCmdAbstract`, `#@modfolder(...)` bundle paths, a 160×160 PNG icon in `UIAtlases/ItemIconAtlas`, and the schematic pattern. **If the GrapplingHook C# source exists on disk, use it as the template ([ASK] Jason for its path).** |
| EAC | Must be off (`SkipWithAntiCheat=true` in ModInfo, like GrapplingHook). The game's assembly is **publicized** (`[PublicizedFrom]` attributes), so protected/private members are directly accessible. Harmony is still needed to *change* behavior. |

---

## 3. Verified engine facts (read before writing code)

### 3.1 Player movement is `vp_FPController` (UFPS-derived) — `D:\dotPeek Test\Assembly-CSharp\vp_FPController.cs`
- `FixedUpdate()` runs in this order: `UpdateMotor() → UpdateJump() → UpdateForces() → UpdateSliding() → UpdateOutOfControl() → FixedMove() → UpdateCollisions() → UpdatePlatformMove()` [VERIFIED ~L309].
- `UpdateForces()` adds gravity to `m_FallSpeed` every step: `m_GravityForce = Physics.gravity.y * (PhysicsGravityModifier * (1/500)) * AdjustedTimeScale` [VERIFIED ~L430].
- `FixedMove()` builds `m_MoveDirection = m_ExternalForce + m_MotorThrottle`, multiplies x/z by `m_SpeedModifier`, adds `m_FallSpeed` to y, then moves by `m_MoveDirection * Delta * Time.timeScale`, where **`Delta = Time.deltaTime * 60`** (`vp_Component.cs` L113) [VERIFIED].
  - ⇒ **Unit conversion: world velocity (m/s) = per-step value × 60.** A descent of **8 m/s ⇒ `m_FallSpeed = -8/60 = -0.13333`**. This is independent of the fixed timestep. Horizontal throttle uses the same rule: glide 4 m/s ⇒ `m_MotorThrottle` horizontal magnitude `4/60 / m_SpeedModifier`.
- `UpdateThrottleWalk()` turns `Player.InputMoveVector` into `m_MotorThrottle`, with air control scaled by `MotorAirSpeed = 0.35` [VERIFIED ~L347].
- `localPlayer` field (an `EntityPlayerLocal`) is on the controller [VERIFIED, used in `UpdateMotor`].
- `m_Grounded`, `Grounded`, `m_MaxHeight`, `m_MaxHeightInitialFallSpeed`, `m_FallSpeed`, `m_MotorThrottle`, `m_ExternalForce`, `m_SpeedModifier` are all accessible fields [VERIFIED].

### 3.2 Player fall damage is height-based, NOT speed-based — critical
- On landing, `UpdateCollisions()` computes `m_FallImpact = sqrt(m_MaxHeightInitialFallSpeed² + 2·|g|·(m_MaxHeight − y)) * 0.8677` [VERIFIED ~L645–655]. `m_MaxHeight` is the **highest point since last grounded**, updated in `FixedMove()` [VERIFIED ~L548–552].
  - ⇒ **Slowing the fall alone does NOT prevent fall damage.** The mod must also (a) keep resetting `m_MaxHeight` to the current height while deployed, **and** (b) block the impact event.
- The impact goes to `EntityPlayerLocal.FallImpact(float speed)` (registered at `EntityPlayerLocal.cs` ~L808 via `vp_FPController.Player.FallImpact2`) [VERIFIED]. That method:
  - returns early if god mode, attached to an entity, or `speed <= 0`
  - scales by the block's `FallDamage`, `EntityPlayer.FallDamageModifier` and `PassiveEffects.FallDamageReduction`
  - sets cvar **`_fallSpeed`** and fires **`MinEventTypes.onSelfFallImpact`**
  - plays the hit-ground sound [VERIFIED `EntityPlayerLocal.cs` ~L3545–3575]
- XML applies the damage: `buffStatusCheck01` → `onSelfFallImpact` → `AddBuff buffPlayerFallingDamage` when `_fallSpeed >= .08` (`buffs.xml` ~L598) [VERIFIED]. `buffPlayerFallingDamage` (~L4433) reads `_fallSpeed` and also reacts to `buffImpactBracing` [VERIFIED].
- `EntityAlive.fallHitGround(...)` (`EntityAlive.cs` ~L3192) also deals `DamageSource.fall` when `-motion.y > 0.85` per tick. At 8 m/s this never triggers, but patch it too as a safety net (§5.4).

### 3.3 Input
- `PlayerActionsLocal` has `Jump`, `MoveForward`, `MoveBack`, `MoveLeft`, `MoveRight`, `Move` (two-axis), `Crouch`, `ToggleFlashlight` (InControl `PlayerAction`) [VERIFIED].
- The local player's actions are at `EntityPlayerLocal.playerInput` (`PlatformManager.NativePlatform.Input.PrimaryPlayer`) [VERIFIED ~L565]. Use `.WasPressed` / `.IsPressed`, and read them in **`Update`** (per frame), not `FixedUpdate`, so taps aren't missed.
- The default F key = `ToggleFlashlight`, hence the switch to double-tap Jump.
- Jump in the air (when not jet-packing) only does vanilla things: re-enables the controller, ladder air-attach. No conflict [VERIFIED `EntityPlayerLocal.cs` ~L2690–2705].
- Precedent for turning the player from code: `EntityVehicle.cs` ~L1244 does `attachedPlayerLocal.vp_FPCamera.Yaw += delta` [VERIFIED].

### 3.4 Existing vanilla "parachute" bits (reuse or avoid)
- `EntityAlive.ParachuteWearing` / `bParachuteWearing` exists. It is reset to false on respawn (`PlayerMoveController.cs` ~L898), but it is **not** in `NetPackageEntityAliveFlags` (only aiming/jumping/crouch/etc.), so it is **not synced**. **Do not rely on it.** Optionally set it for vanilla code paths, but drive everything from our own state.
- Vanilla sounds usable as-is (`sounds.xml` ~L12720) [VERIFIED]:
  - `Parachute_Flutter_Lp` (looping flutter): **use it for the in-flight loop**
  - `Parachute_Opens` (3 variants): fallback deploy sound
  - AudioSource prefab `@:Sounds/Prefabs/AudioSource_Parachute.prefab`
- The supply crate's parachute (`EModelSupplyCrate`, transform `parachute_supplies`) is part of the crate prefab and not a reusable standalone asset. Build our own.

### 3.5 Items / XML conventions [VERIFIED in v3.3 XML]
- Chest armor mods: `item_modifiers.xml`, e.g. `modArmorBandolier installable_tags="armorChest" modifier_tags="..." blocked_tags="noMods" type="attachment"`, `Extends="modGeneralMaster" param1="CustomIcon"`.
- Equip detection via buff, the Impact Bracing pattern (`item_modifiers.xml` ~L2131, `buffs.xml` ~L9008):
  ```xml
  <effect_group tiered="false">
    <requirement name="IsEquipped"/>
    <triggered_effect trigger="onSelfEquipStart" action="AddBuff" buff="buffImpactBracing"/>
    <triggered_effect trigger="onSelfEquipStop"  action="RemoveBuff" buff="buffImpactBracing"/>
  </effect_group>
  ```
- Vanilla dev-only chest mod `modArmorJumpJets` (~L2732) is a useful reference for air-movement mods.
- Schematic pattern: `modArmorBandolierSchematic` (`items.xml` ~L23190) extends `schematicNoQualityMaster`, sets `Unlocks`, and on read sets cvar `<modName>=1` and gives 50 XP.
- T2 loot group: `<lootgroup name="schematicsModsAndGeneralT2">` (`loot.xml` ~L2838) already contains `modArmorImpactBracingSchematic`, `modArmorCustomizedFittingsSchematic`, etc.
- Trader template for tier-2 mods: `TraderStageTemplate="modsTier2"` (as Impact Bracing).
- Recipe style: `<recipe name="..." count="1" craft_area="workbench" tags="learnable,workbenchCrafting">`.
- Localization CSV header used by the GrapplingHook mod: `Key,File,Type,UsedInMainMenu,NoTranslate,english`. Vanilla also has `KeepLoaded`, `Context / Alternate Text`, but columns are matched by header, so the short form works.
- Mod slot counts come from `passive_effect name="ModSlots"` by quality. E.g. Scavenger outfit gives `2,2,3,3,4,5` for Q1–6 [VERIFIED]. ⇒ spawn test outfits at **Q6** to guarantee free slots.

### 3.6 Modding API [VERIFIED]
- `IModApi.InitMod(Mod _modInstance)`.
- `ModEvents.PlayerSpawnedInWorld` gives `SPlayerSpawnedInWorldData` with `IsLocalPlayer` (constructor arg `entityPlayer is EntityPlayerLocal`) [`GameManager.cs` ~L2996]. Also `GameStartDone`, `WorldShuttingDown`, `GameUpdate`.
- Console commands: subclass `ConsoleCmdAbstract`. Implement `getCommands()`, `getDescription()`, `getHelp()`, `Execute(List<string>, CommandSenderInfo)`. Override `IsExecuteOnClient => true` for local-player commands (precedent: `ConsoleCmdBuff`). The game auto-discovers commands in mod DLLs (GrapplingHook's `ConsoleCmdGrapplingHook` proves it).
- Giving items: `ItemClass.GetItem(name, true)` → `new ItemValue(type, minQ, maxQ, createDefaultMods)` → `new ItemStack(iv, count)`. For the local player use `LocalPlayerUI.GetUIForPlayer(player).xui.PlayerInventory.AddItem(stack)`, which falls back to a drop if full. Vanilla `give` uses `GameManager.Instance.ItemDropServer(...)` [VERIFIED `ConsoleCmdGive.cs`].
- Asset paths: `DataLoader.LoadAsset<T>("#@modfolder(Parachute):Resources/Parachute.unity3d?AssetName")`. `#` = bundle, `?` separates the asset name [VERIFIED `DataLoader.ParseDataPathIdentifier`]. In XML, write `@modfolder:` and `XmlPatcher` rewrites it to `@modfolder(<ModName>):` [VERIFIED `XmlPatcher.cs` L75].
- Audio: `Audio.Manager.PlayInsidePlayerHead(string, int entityId)`, `Audio.Manager.BroadcastPlay(Entity, Vector3, string)`, `Entity.PlayOneShot(...)` [VERIFIED `Audio/Manager.cs`].
- UI-open check: `GUIWindowManager.IsModalWindowOpen()`, `IsInputActive()`, `IsCursorWindowOpen()` [VERIFIED].

---

## 4. Deliverable file tree

```
C:\Users\Jason\Projects\7d2d\Parachute\   ← source repo root
├─ plan.md                                   ← this file
├─ src/Parachute/
│  ├─ Parachute.csproj                       (net48, refs per §2, post-build copy to game Mods)
│  ├─ ParachuteMod.cs                        (IModApi entry; Harmony PatchAll; ModEvents hooks)
│  ├─ ParachuteConfig.cs                     (JSON-with-comments loader, defaults, clamping, reload)
│  ├─ ParachuteState.cs                      (per-local-player state machine; single source of truth)
│  ├─ ParachuteController.cs                 (MonoBehaviour on local player: input, double-tap, auto-stow, audio)
│  ├─ ParachuteFlight.cs                     (pure math: target velocities, yaw rate; unit-testable)
│  ├─ ParachuteVisual.cs                     (canopy instantiate/animate/destroy; bank & sway)
│  ├─ ParachuteAssets.cs                     (bundle loading + caching + fallbacks)
│  ├─ Patches/
│  │  ├─ Patch_vpFPController.cs             (UpdateThrottleWalk + UpdateForces postfixes)
│  │  ├─ Patch_FallImpact.cs                 (EntityPlayerLocal.FallImpact prefix)
│  │  └─ Patch_FallHitGround.cs              (EntityAlive.fallHitGround prefix, safety net)
│  ├─ Commands/ConsoleCmdParachute.cs        (`chute ...`)
│  └─ Net/ (Phase 2 only) NetPackageParachuteState.cs, RemoteParachuteRegistry.cs
├─ unity/ParachuteAssets/                    (Unity 2022.3.62f2 project for the bundle)
│  ├─ Assets/Parachute/Models/ParachuteCanopy.fbx (or .blend export)
│  ├─ Assets/Parachute/Materials/Canopy.mat, Lines.mat
│  ├─ Assets/Parachute/Prefabs/ParachuteCanopy.prefab
│  ├─ Assets/Parachute/Audio/parachute_deploy.wav
│  └─ Assets/Editor/BuildBundles.cs, RenderIcon.cs
├─ tools/
│  ├─ make_deploy_sound.py                   (procedural SFX generator → WAV)
│  └─ deploy.ps1                             (copy Mod/ to C:\7D2D\Custom\v3.3\Mods\Parachute)
└─ Mod/Parachute/                            ← what ships (copied into the game's Mods folder)
   ├─ ModInfo.xml
   ├─ Parachute.dll
   ├─ Config/
   │  ├─ ParachuteConfig.json
   │  ├─ item_modifiers.xml
   │  ├─ items.xml
   │  ├─ recipes.xml
   │  ├─ loot.xml
   │  ├─ buffs.xml
   │  ├─ sounds.xml
   │  └─ Localization.csv
   ├─ Resources/Parachute.unity3d
   └─ UIAtlases/ItemIconAtlas/modArmorParachute.png   (160×160 RGBA)
```

**Names (use these exactly; they are referenced across files):**

| Name | What |
|---|---|
| `Parachute` | Mod `Name` in ModInfo (drives `@modfolder(Parachute)`) |
| `modArmorParachute` | the mod item (item_modifier) |
| `modArmorParachuteSchematic` | schematic item; also the cvar set on read |
| `buffParachuteHarness` | hidden buff: mod is installed in equipped chest armor |
| `buffParachuteDeployed` | hidden buff: canopy currently open (used by XML fall-damage guard; syncs in MP) |
| `parachute_deploy` | SoundDataNode for the custom deploy SFX |
| `Parachute_Flutter_Lp` | vanilla loop, reused |
| `ParachuteCanopy` | prefab name inside the bundle |
| `ParachuteDeploy` | AudioClip asset name inside the bundle |
| `chute` | console command root |

---

## 5. Detailed design

### 5.1 State machine (`ParachuteState`, local player)

```
           double-tap Jump & CanDeploy()                 deploy anim done (deploy_time)
 Stowed ───────────────────────────────▶ Deploying ─────────────────────────────▶ Open
   ▲                                         │                                     │
   │  landed / water / ladder / vehicle /    │ (same auto-stow triggers)           │ double-tap Jump & allow_cutaway
   │  death / respawn / mod removed / fly    ▼                                     ▼
   └────────────── Stowing (collapse anim, stow_anim_time) ◀───────────────────────┘
```

`CanDeploy()` is true only if **all** of these hold:
1. `player.Buffs.HasBuff("buffParachuteHarness")`. Secondary check (belt and braces): the chest slot (`EquipmentSlots.Chest = 1`) item's `Modifications[]` contains `modArmorParachute`.
2. Not grounded (`vp_FPController.Grounded == false`) and `!player.onGround`.
3. Clear air below ≥ `min_deploy_height`, checked with `Physics.Raycast` down from the feet against terrain/blocks (use the same layer mask as the controller's ground SphereCast, `1084850184`).
4. Not swimming or in water (`player.IsInWater()` / `IsSwimming()`), not on a ladder (`isLadderAttached`), not attached to a vehicle (`AttachedToEntity == null`), not dead, not in fly mode (`IsFlyMode.Value`), not god mode, `JetpackWearing == false`.
5. No modal or cursor UI open (`playerUI.windowManager.IsModalWindowOpen()` / `IsCursorWindowOpen()` / chat input active).
6. Not in the stow cooldown (`redeploy_cooldown_ms`, default 300 ms after a stow).

**Double-tap detection:** in `Update()`, on `playerInput.Jump.WasPressed`, if the previous press was ≤ `double_tap_window_ms` ago **and both presses happened while airborne**, fire a toggle and clear the timestamp. Otherwise store the timestamp. A ground jump plus one air tap therefore never deploys.

**Optional direct key:** if config `deploy_key` is a valid `UnityEngine.KeyCode` name (e.g. `"G"`), `Input.GetKeyDown` on it also toggles. Empty string (default) = disabled.

**Auto-stow** is checked every frame: grounded (or `onGround`), in water, ladder, vehicle, dead, harness buff gone, fly/god mode, or teleported (position delta > 50 m in one frame).

When `Open`/`Deploying` → grounded, **set `landingGraceUntil = now + landing_grace_ms`** before stowing so fall-impact suppression still covers the landing frame (§5.4).

### 5.2 Flight model (`ParachuteFlight`, pure functions; per FixedUpdate step)

Inputs: `fwd = MoveForward.IsPressed`, `back = MoveBack.IsPressed`, `left`, `right` (read the `PlayerAction`s directly, not `InputMoveVector`, so analog sticks also work via `Move.Y`/`Move.X`; use `Move.Value` with a 0.3 deadzone).

| Control | Horizontal glide speed (m/s) | Descent (m/s) |
|---|---|---|
| neutral | `glide_speed` (4.0) | `fall_speed` (**4.0**) |
| Forward | `glide_speed_forward` (6.0) | `fall_speed_forward` (5.5) — "speed up slightly" |
| Back (flare/glide) | `glide_speed_back` (1.5) | `fall_speed_back` (2.75) — "slow down/glide" |

- Turning: `yawDelta = turn_rate (deg/s) × Time.deltaTime × (right − left)`, applied in `Update()` as `player.vp_FPCamera.Yaw += yawDelta` (vehicle precedent). Mouse look still works and also steers.
- Horizontal direction = the player body's forward on the XZ plane (`vp_FPController.Transform.forward`, y = 0, normalized).
- Smoothing: the current horizontal and vertical speeds approach their targets with `Mathf.MoveTowards` at `control_response` m/s² (default 6).
- **Deploy brake:** while `Deploying`, the descent target ramps from the current fall speed to `fall_speed` over `deploy_time` seconds (default 0.8) using ease-out. This gives an "opening shock" without an instant stop. Never let the descent go *above* the current speed during the ramp (no upward kick).
- If the player is moving upward when deploying (impossible with the min-height and airborne rules, but guard anyway), let gravity act until the vertical speed ≤ 0.

### 5.3 Harmony patches on `vp_FPController` (`Patch_vpFPController.cs`)

Each patch applies only when `__instance.localPlayer` is the local player **and** `ParachuteState.IsActive` (Deploying or Open).

1. **Postfix `UpdateThrottleWalk()`**: override horizontal throttle:
   ```csharp
   Vector3 dir = flatForward * (curGlideSpeed / 60f);
   float sm = Mathf.Max(0.01f, __instance.m_SpeedModifier);   // FixedMove multiplies x/z by this
   __instance.m_MotorThrottle.x = dir.x / sm;
   __instance.m_MotorThrottle.z = dir.z / sm;
   ```
   This also suppresses vanilla strafing (Left/Right become turning).
2. **Postfix `UpdateForces()`**: clamp vertical speed and erase fall-height memory:
   ```csharp
   float target = -curFallSpeed / 60f;                          // per-step units (§3.1)
   if (__instance.m_FallSpeed < target) __instance.m_FallSpeed = target;   // only brake, never push down
   __instance.m_ExternalForce.y = Mathf.Min(__instance.m_ExternalForce.y, 0f) * 0.5f; // damp external vertical shoves
   float y = __instance.Transform.position.y + Origin.position.y;
   __instance.m_MaxHeight = y;                                  // fall height measured from *now*
   __instance.m_MaxHeightInitialFallSpeed = 0f;
   ```
   `curFallSpeed` comes from the ramp in §5.2. Note: in this game the vertical "speed" lives in `m_FallSpeed`, not the throttle.
3. **[VERIFY] in-game** that `UpdateJumpForceWalk` doesn't add lift. It only does so when `Velocity.y >= 0`, which won't happen during descent.

Use `[HarmonyPatch(typeof(vp_FPController), "UpdateThrottleWalk")]` etc. The methods are `virtual`, so patch the declared type. Confirm no subclass overrides them in v3.3 (`grep "override void UpdateForces"`). None were seen during research.

### 5.4 Fall-damage immunity (three layers, R7)
1. **Primary:** the `m_MaxHeight` reset in §5.3 makes the computed `m_FallImpact` ≈ the impact of falling from ~0 m.
2. **Prefix `EntityPlayerLocal.FallImpact(float speed)`** → `return false` (skip the original) when `ParachuteState.IsActive || Time.time < landingGraceUntil`. This blocks `_fallSpeed`, `onSelfFallImpact` and the hard-landing sound. Optionally play a soft landing sound instead.
3. **XML guard** (`Config/buffs.xml`): add `<requirement name="!HasBuff" buff="buffParachuteDeployed"/>` to the `onSelfFallImpact → buffPlayerFallingDamage` trigger in `buffStatusCheck01`:
   ```xml
   <append xpath="/buffs/buff[@name='buffStatusCheck01']//triggered_effect[@trigger='onSelfFallImpact' and @buff='buffPlayerFallingDamage']">
     <requirement name="!HasBuff" buff="buffParachuteDeployed"/>
   </append>
   ```
   **[VERIFIED]** This trigger appears exactly once in vanilla `buffs.xml` (~L598), inside `buffStatusCheck01` (opens ~L330). The `//` makes the match independent of which `effect_group` holds it. Still check the game log for XPath warnings. Remove `buffParachuteDeployed` **after** the landing grace period, not on the landing frame.
4. **Safety net:** prefix `EntityAlive.fallHitGround` → when `this` is the local player and the parachute is active or in grace, call through with `_fallMotion.y` clamped to `Mathf.Max(_fallMotion.y, -0.5f)`. Simpler alternative: skip the damage portion; reproduce the base call to `Entity.fallHitGround` (an empty virtual) and the landing animation call. Prefer the clamping approach so vanilla side effects (landing anim, AI notify) still run.

### 5.5 Buffs (`Config/buffs.xml`)
```xml
<configs>
  <append xpath="/buffs">
    <buff name="buffParachuteHarness" hidden="true">
      <stack_type value="ignore"/><duration value="0"/>
    </buff>
    <buff name="buffParachuteDeployed" hidden="true">
      <stack_type value="ignore"/><duration value="0"/>
    </buff>
  </append>
  <!-- + fall-damage guard from §5.4 -->
</configs>
```
C# adds and removes `buffParachuteDeployed` (`player.Buffs.AddBuff("buffParachuteDeployed")` / `RemoveBuff`) on deploy/stow. `EntityBuffs.AddBuff(...)` has two overloads returning `EntityBuffs.BuffStatus` [VERIFIED `EntityBuffs.cs` L148/L158]. Read their parameter lists before calling, and log any non-success status.

### 5.6 Item modifier (`Config/item_modifiers.xml`)
```xml
<configs>
  <append xpath="/item_modifiers">
    <item_modifier name="modArmorParachute" installable_tags="armorChest" modifier_tags="parachute" blocked_tags="noMods" type="attachment">
      <property name="Extends" value="modGeneralMaster" param1="CustomIcon"/>
      <property name="CustomIcon" value="modArmorParachute"/>
      <property name="DescriptionKey" value="modArmorParachuteDesc"/>
      <property name="UnlockedBy" value="modArmorParachuteSchematic"/>
      <property name="TraderStageTemplate" value="modsTier2"/>
      <property name="Material" value="Mcloth"/>
      <property name="EconomicValue" value="900"/>
      <effect_group tiered="false">
        <requirement name="IsEquipped"/>
        <triggered_effect trigger="onSelfEquipStart" action="AddBuff" buff="buffParachuteHarness"/>
        <triggered_effect trigger="onSelfEquipStop"  action="RemoveBuff" buff="buffParachuteHarness"/>
      </effect_group>
    </item_modifier>
  </append>
</configs>
```
- `modifier_tags="parachute"` is unique, so two parachutes can't stack on one item.
- Do **not** set `ShowQuality`. The mod is quality-less like Impact Bracing.
- **[VERIFY]** whether `modGeneralMaster` already provides `DescriptionKey` handling. If the description doesn't show, the convention is `<key>Desc` auto-lookup. Keep the explicit property anyway.

### 5.7 Schematic (`Config/items.xml`)
```xml
<configs>
  <append xpath="/items">
    <item name="modArmorParachuteSchematic">
      <property name="Extends" value="schematicNoQualityMaster"/>
      <property name="CreativeMode" value="Player"/>
      <property name="CustomIcon" value="modArmorParachute"/>
      <property name="Unlocks" value="modArmorParachute"/>
      <effect_group tiered="false">
        <triggered_effect trigger="onSelfPrimaryActionEnd" action="ModifyCVar" cvar="modArmorParachute" operation="set" value="1"/>
        <triggered_effect trigger="onSelfPrimaryActionEnd" action="GiveExp" exp="50"/>
      </effect_group>
    </item>
  </append>
</configs>
```

### 5.8 Recipe (`Config/recipes.xml`), tier-2 workbench
```xml
<configs>
  <append xpath="/recipes">
    <recipe name="modArmorParachute" count="1" craft_area="workbench" craft_time="60" tags="learnable,workbenchCrafting">
      <ingredient name="resourceCloth" count="80"/>
      <ingredient name="resourceLeather" count="10"/>
      <ingredient name="resourceDuctTape" count="3"/>
      <ingredient name="resourceSewingKit" count="1"/>
      <ingredient name="resourceMechanicalParts" count="4"/>
    </recipe>
  </append>
</configs>
```
Ingredient names all exist in v3.3 (used by `modArmorBandolier`, `modArmorStoragePocket`, `modArmorImpactBracing`) [VERIFIED]. The balance is a starting point; see §12.

### 5.9 Loot (`Config/loot.xml`)
```xml
<configs>
  <append xpath="/lootcontainers/lootgroup[@name='schematicsModsAndGeneralT2']">
    <item name="modArmorParachuteSchematic"/>
  </append>
</configs>
```
Optional (off by default, mention in the changelog): add to trader stock for tier-2 mods only if Jason wants it. The user chose loot only.

### 5.10 Localization (`Config/Localization.csv`)
```
Key,File,Type,UsedInMainMenu,NoTranslate,english
modArmorParachute,item_modifiers,mod,,,Parachute Mod
modArmorParachuteDesc,item_modifiers,mod,,,"A packed, steerable canopy stitched into a harness for chest armor.\n\nWhile falling, double-tap Jump to deploy. Hold Forward to dive a little faster, hold Back to flare and glide, and use Left and Right to turn. You take no fall damage while the canopy is open.\n\nDouble-tap Jump again to cut away."
modArmorParachuteSchematic,item_modifiers,mod,,,Parachute Mod Schematic
```
Make the description text match the actual config behavior (cutaway on/off). Re-check it after tuning.

### 5.11 Sounds (`Config/sounds.xml`)
```xml
<configs>
  <append xpath="/Sounds">
    <SoundDataNode name="parachute_deploy">
      <AudioSource name="@:Sounds/Prefabs/AudioSource_Parachute.prefab"/>
      <AudioClip ClipName="#@modfolder:Resources/Parachute.unity3d?ParachuteDeploy"/>
      <LocalCrouchVolumeScale value="1"/> <CrouchNoiseScale value="0.5"/> <NoiseScale value="1"/>
      <MaxVoices value="4"/> <MaxRepeatRate value="0.2"/>
    </SoundDataNode>
  </append>
</configs>
```
Root element is `<Sounds>` [VERIFIED]. **[VERIFY]** that `ClipName` resolves bundle URIs through `DataLoader` (`SoundsFromXml.cs` stores `clipName`; follow it into `Audio/Manager.cs` / `ClipSourceMap.cs`). If it does not, load the clip from C# via `ParachuteAssets` and play it through `AudioSource.PlayClipAtPoint` attached to the player as the fallback, and keep the XML node using vanilla `Parachute_Opens` clips so something still plays.

Playback:
- Deploy: `Audio.Manager.PlayInsidePlayerHead("parachute_deploy", player.entityId)`. Phase 2: `Audio.Manager.BroadcastPlay(player, pos, "parachute_deploy")` so others hear it.
- In flight: start the vanilla `Parachute_Flutter_Lp` loop on Open. Stop it on stow via `Audio.Manager.Stop(int entityId, string soundGroupName)` [VERIFIED `Audio/Manager.cs` L987]. For loops, also check `StopGroupLoop(...)` (L1025). The volume can scale with descent speed if the API allows; otherwise use a constant volume.

### 5.12 Config (`Config/ParachuteConfig.json`): JSON with `//` comments, same style as GrapplingHook
Load with `Newtonsoft.Json` using `JsonTextReader` + `JsonLoadSettings { CommentHandling = Ignore }`. Every key has a default, an allowed range, and a log line when a value is invalid. `chute reload` re-reads it.

| Key | Default | Range | Meaning |
|---|---|---|---|
| `deploy_input` | `"double_tap_jump"` | `double_tap_jump` \| `key` \| `both` | how to deploy |
| `deploy_key` | `""` | Unity `KeyCode` name or empty | optional direct key (empty = off; avoid `F`, the flashlight key) |
| `double_tap_window_ms` | 350 | 150–1000 | max gap between the two Jump taps |
| `min_deploy_height` | 3.0 | 0–50 | clear air (m) below the feet required to deploy |
| `fall_speed` | **4.0** | 1–30 | neutral descent, m/s |
| `fall_speed_forward` | 5.5 | 1–30 | descent while holding Forward |
| `fall_speed_back` | 2.75 | 1–30 | descent while holding Back |
| `glide_speed` | 4.0 | 0–20 | neutral forward speed, m/s |
| `glide_speed_forward` | 6.0 | 0–25 | forward speed while holding Forward |
| `glide_speed_back` | 1.5 | 0–20 | forward speed while holding Back |
| `turn_rate` | 75 | 10–360 | deg/s from Left/Right |
| `control_response` | 6.0 | 0.5–50 | m/s² smoothing toward target speeds |
| `deploy_time` | 0.8 | 0.1–3 | seconds to brake to `fall_speed` and finish the open animation |
| `stow_anim_time` | 0.4 | 0.05–2 | canopy collapse animation time |
| `allow_cutaway` | true | bool | double-tap again while open to stow |
| `redeploy_cooldown_ms` | 300 | 0–5000 | after stowing |
| `landing_grace_ms` | 500 | 0–3000 | fall-damage immunity after touchdown |
| `canopy_scale` | 1.0 | 0.25–3 | visual size multiplier |
| `canopy_height` | 3.2 | 1–8 | canopy center above the player's feet origin (m) |
| `left_right` | `"strafe"` | `strafe` \| `turn` | Left/Right slide sideways, or rotate the player |
| `strafe_speed` | 3.0 | 0–15 | sideways speed in strafe mode, m/s |
| `play_flutter_loop` | true | bool | vanilla flutter loop while open |
| `flutter_pitch` | 0.7 | 0.3–1.5 | pitch of the flutter loop; 1.0 is vanilla, lower is deeper and slower |
| `debug` | false | bool | verbose logging and on-screen readout |

Log prefix for every message: `[Parachute]`, the same convention as `[GrapplingHook]`.

### 5.13 Visuals (`ParachuteVisual` + asset)
- On `Deploying`: instantiate `ParachuteCanopy` (cache the prefab after first load) and parent it to the **player entity root transform** (`player.transform`; it follows the physics position in first and third person). Local position `(0, canopy_height, -0.2)`.
- Animation is driven by code; no Animator is needed:
  - Open: local scale `(0.15, 0.4, 0.15)` → `(1,1,1) × canopy_scale` over `deploy_time` with an overshoot ease (back-out). Pitch starts at −30° and settles to 0.
  - Idle: subtle sway, pitch/roll ±2° sine at 0.6 Hz with phase offsets.
  - Turning: roll toward the turn direction, `turnInput × 12°`, smoothed. Forward: pitch +6°. Back: pitch −8°.
  - Stow: scale y → 0.1 and xz → 0.3 over `stow_anim_time`, then `Destroy`.
- **The prefab must contain no Colliders and no Rigidbodies.** The controller's ground/ceiling SphereCasts use layer mask `1084850184`, so any collider could make the player "land" on their own canopy. Set every child's layer to **0 (Default)**. **[VERIFY]** that the first-person camera renders layer 0. If the canopy is invisible in first person, check `playerCamera.cullingMask` and pick a layer it includes that is *not* in `1084850184`.
- Suspension lines: a `LineRenderer` per riser group (4 groups is enough), from canopy anchor points to two shoulder points (`player.emodel.GetRightHandTransform()`-style lookups are overkill; use fixed local offsets `(±0.25, 1.4, 0)`). Update positions in `LateUpdate`.
- In first person, the player sees the lines converge at their shoulders and the canopy overhead when looking up. This is the intended visibility.

### 5.14 Assets to create

**A. Canopy model** (Blender or Unity ProBuilder; export FBX)
- Ram-air (rectangular, steerable) canopy: span ≈ 7 m, chord ≈ 2.6 m, 7 cells, arched (anhedral ~25° at the tips). Visible cell openings on the leading edge, closed trailing edge.
- Build double-sided geometry, because the Standard shader culls back faces. Budget ≤ 3,000 tris, with UVs.
- Pivot at the canopy center-bottom. Forward = +Z, up = +Y, so it matches Unity.
- Materials: Standard shader. Base color faded olive/tan with alternating cell panels (e.g. olive `#5B5A3A` and sand `#B9A77C`), smoothness 0.15, plus an optional subtle fabric normal map (≤ 1024²).
- **[VERIFY]** that the Standard shader renders correctly in game. The usual community practice works because the shader is compiled into the bundle. If it renders pink, switch to a game shader by name at load time (`Shader.Find("Game/...")`) or use `Legacy Shaders/Diffuse`.

**B. Prefab `ParachuteCanopy`:** root (empty) → `Canopy` mesh + four `LineRenderer`s (`Lines.mat`: Unlit/Color, dark grey, width 0.015 m) → named empty anchors `LineAnchor_0..N` on the canopy. No colliders.

**C. Inventory icon `modArmorParachute.png`:** 160×160 RGBA with a transparent background, matching vanilla mod icon style (a 3/4 view object on transparent).
- Content: a packed parachute harness/pack with a folded canopy edge and two shoulder straps, olive/tan, with a mild dark outline so it reads on the dark inventory UI.
- How to produce it: `Assets/Editor/RenderIcon.cs` renders a "packed" variant (or the canopy at 3/4 view) with an orthographic camera to a 160×160 RenderTexture (ARGB32, transparent clear), then `EncodeToPNG`. Touch up by hand if needed.
- The same icon is used by the schematic (the vanilla schematic pattern reuses the mod icon).

**D. Deploy sound `parachute_deploy.wav`:** created procedurally by `tools/make_deploy_sound.py` (numpy + scipy or the stdlib `wave` module). 44.1 kHz, 16-bit, **mono** (3D-positional), ~1.45 s, peak −1 dBFS. Layers:
1. 0.00–0.25 s: pack release, a short velcro/zip rip. Band-passed noise at 0.7–2.5 kHz with an amplitude flutter of 38 Hz.
2. 0.15–0.85 s: fabric unfurl whoosh. Pink noise through a low-pass that sweeps 2.5 kHz → 300 Hz, rising then falling envelope, with light random tremolo for "flapping".
3. ~0.80 s: canopy snap/inflation thump. A 55→32 Hz sine (plus a quiet octave above) with a 200 ms exponential decay plus a 10 ms filtered noise transient, followed by a short 400 ms fabric tail.

   Use a fixed RNG seed so the output is reproducible, then normalize and apply 5 ms fades. Import into Unity as `ParachuteDeploy` (Load Type: Decompress On Load, Vorbis q=70, Force To Mono).

**E. Bundle build:** `Assets/Editor/BuildBundles.cs` menu item → `BuildPipeline.BuildAssetBundles(outDir, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64)`. Bundle name `parachute.unity3d`, renamed or copied to `Mod/Parachute/Resources/Parachute.unity3d`. Asset names inside must be exactly `ParachuteCanopy` and `ParachuteDeploy`. **The Unity editor version must be exactly 2022.3.62f2.**

### 5.15 Console commands (`ConsoleCmdParachute`, root `chute`, aliases `parachute`)
`IsExecuteOnClient => true`. Operates on the **local** player (`GameManager.Instance.World.GetPrimaryPlayer()`). If run on a dedicated server console, reply "must be run from a game client".

| Command | Effect |
|---|---|
| `chute give` | gives all three test items: outfit + schematic + mod |
| `chute outfit [itemName] [quality]` | gives a chest outfit. Default `armorScavengerOutfit` Q6 (5 mod slots). Validates the item has the `armorChest` tag |
| `chute mod [count]` | gives `modArmorParachute` |
| `chute schematic [count]` | gives `modArmorParachuteSchematic` |
| `chute kit` | gives the outfit with the parachute mod **pre-installed** (set `itemValue.Modifications[firstEmpty]`) for quick flight tests |
| `chute up [meters]` | teleports the player straight up (default 60 m) for drop tests (`player.SetPosition` / `TeleportToPosition`; **[VERIFY]** API) |
| `chute deploy` / `chute stow` | forces the state, ignoring the input rules except "has harness" (debug) |
| `chute status` | prints harness/deployed/state, current speeds, config values |
| `chute reload` | reloads `ParachuteConfig.json` |
| `chute debug [on\|off]` | toggles debug logging and the HUD readout (descent m/s, glide m/s, height above ground) |

Item creation: `new ItemValue(ItemClass.GetItem(name).type, q, q, false)` then `xui.PlayerInventory.AddItem(new ItemStack(iv, n))`. If that returns false, drop at the player's feet with `GameManager.Instance.ItemDropServer(stack, pos, Vector3.zero, player.entityId, 60f, false)`. Print exactly what was given.

### 5.16 Mod entry (`ParachuteMod : IModApi`)
- `InitMod(Mod mod)`:
  - store `mod.Path`
  - load the config
  - `new Harmony("jason.parachute").PatchAll(Assembly.GetExecutingAssembly())`
  - subscribe to `ModEvents.PlayerSpawnedInWorld`: if `IsLocalPlayer`, ensure exactly one `ParachuteController` component exists on the local player's GameObject and reset its state
  - subscribe to `ModEvents.WorldShuttingDown`: stow and destroy visuals, clear caches
- Log `[Parachute] Loaded v{version}; {n} patches applied` and list any failed patches loudly.

### 5.17 `ModInfo.xml`
```xml
<?xml version="1.0" encoding="UTF-8" ?>
<xml>
  <Name value="Parachute" />
  <DisplayName value="Parachute" />
  <Description value="A chest-armor mod that lets you deploy a steerable parachute in mid-air." />
  <Author value="Jason" />
  <Version value="0.1.0" />
  <Website value="" />
  <SkipWithAntiCheat value="true" />
</xml>
```

---

## 6. Multiplayer

### Phase 1 (v0.x): single-player / listen-host only
- All logic runs on the local client. Fall damage is evaluated locally for the local player (§3.2), so immunity works for the host and for any client that also has the DLL.
- Remote players do **not** see others' canopies (accepted limitation).
- XML (items, recipes, loot, buffs) must be on the host as usual.

### Phase 2 (v1.0): full MP + dedicated server
1. **`NetPackageParachuteState`** (`NetPackage` subclass) carries `entityId` and `state` (byte: Stowed/Deploying/Open/Stowing), plus `yawInput` (sbyte) and `forward/back` (bits) for canopy banking.
   - Client → server on every state change (and input changes at most 10 Hz).
   - Server → all clients except the sender: `SendPackage(..., _allButAttachedToEntityId: entityId)`.
   - **[VERIFY]** the v3.3 `NetPackage` abstract members (`read(PooledBinaryReader)`, `write(PooledBinaryWriter)`, `ProcessPackage(World, GameManager)`, `GetLength()`) in `NetPackage.cs`, and copy a small vanilla package (e.g. `NetPackageEntityStealth`) as the template. Custom NetPackages in mod DLLs are auto-registered by type scan. **[VERIFY]** in `NetPackageManager.cs`.
2. **`RemoteParachuteRegistry`**: on receipt, attach or detach a `ParachuteVisual` on that `EntityPlayer` (non-local). Animate banking from the replicated input. Clean up on `EntityKilled` / `PlayerDisconnected` / despawn.
3. **Sound for others:** use `Audio.Manager.BroadcastPlay(entity, pos, "parachute_deploy")` instead of `PlayInsidePlayerHead`.
4. **Late joiners:** on `PlayerSpawnedInWorld` (server side), send the current open-chute states to the joining client.
5. **Dedicated server:** the DLL must load headless. Guard every Unity rendering/audio call with `GameManager.IsDedicatedServer` and never load the bundle on a dedicated server.
6. **Authority/anti-abuse (light):** the server ignores `Open` states from players who don't have `buffParachuteHarness`. `buffParachuteDeployed` is a normal buff and replicates.
7. **Test matrix:** listen host + 1 client, dedicated + 2 clients, client with a high ping (clumsy/latency sim), late join while someone is in the air.

---

## 7. Build & deploy
- `Parachute.csproj`: `TargetFramework net48`, `LangVersion latest`, `Optimize`. Every game reference `Private=false` (do not copy game DLLs). Reference `0Harmony` from `Mods\0_TFP_Harmony`.
- Post-build: copy `Parachute.dll` (+ `.pdb` in Debug) into `Mod\Parachute\`. Then `tools\deploy.ps1` mirrors `Mod\Parachute\` → `C:\7D2D\Custom\v3.3\Mods\Parachute\` (robocopy /MIR). **Never touch other mods.**
- Game path in one MSBuild property, `GamePath=C:\7D2D\Custom\v3.3`, so it's easy to change.
- Launch **without EAC** (`7DaysToDie.exe`, or Jason's mod launcher; ModLauncher config shows `UseEAC=false`).
- Logs: `%APPDATA%\7DaysToDie\logs\` or `7DaysToDie_Data\output_log*.txt`. **[VERIFY]** which one is used on this install. Search for `[Parachute]`, `XML`, `Exception`.

---

## 8. Phased task list (do in order; each phase ends green before the next)

### Phase 0: scaffolding
- [ ] Create the repo layout from §4. Add `.gitignore` (bin/obj, Unity Library/Temp/Logs).
- [ ] csproj builds an empty `IModApi` that logs `[Parachute] Loaded`.
- [ ] `ModInfo.xml`, deploy script.
- **Accept:** game starts, the log shows `[Parachute] Loaded`, no errors.

### Phase 1: XML content (no C# behavior yet)
- [ ] `item_modifiers.xml`, `items.xml`, `recipes.xml`, `loot.xml`, `buffs.xml` (both buffs + the §5.4 guard), `Localization.csv`, placeholder icon PNG.
- **Accept:**
  - The creative menu shows the mod and the schematic, with correct names and descriptions.
  - The mod fits **only** chest armor.
  - Reading the schematic unlocks the workbench recipe.
  - With the mod installed, `buffParachuteHarness` is active (check with `buff`/debug).
  - The log has no XPath "0 matches" warnings.

### Phase 2: console commands
- [ ] Implement §5.15 (`give`, `outfit`, `mod`, `schematic`, `kit`, `up`, `status`, `reload`, `debug`).
- **Accept:** each command adds the right items. The outfit is Q6 with free slots. `chute kit` arrives with the mod installed. `chute up 80` lifts the player 80 m.

### Phase 3: config
- [ ] `ParachuteConfig` with defaults, ranges, comments in the shipped file, `chute reload`.
- **Accept:** an invalid value logs a warning and falls back. Reload changes behavior live.

### Phase 4: deploy logic & flight
- [ ] `ParachuteController` (input, double-tap, CanDeploy, auto-stow), `ParachuteState`, `ParachuteFlight`.
- [ ] Harmony patches §5.3.
- **Accept** (use `chute debug on`, which shows descent m/s):
  - Steady descent reads **8.0 ± 0.3 m/s** neutral, about 9 with Forward, about 5.5 with Back.
  - Glide speeds match the config.
  - A/D yaws the player at about `turn_rate`.
  - A ground hop plus double-tap doesn't deploy. A double-tap in the air above `min_deploy_height` does.
  - Cutaway works. Auto-stow works on water, ladder and landing.

### Phase 5: fall-damage immunity
- [ ] Patches §5.4 (prefix FallImpact, fallHitGround safety net, grace window, deployed buff).
- **Accept:**
  - From `chute up 100`: falling without the chute = vanilla damage or death. Deploying at any height (including 3–5 m above ground, mid-brake) = **0 damage**, no leg injury buffs.
  - Cutting away at 20 m = normal fall damage from 20 m. This proves `m_MaxHeight` restarts at the cutaway point.
  - Impact Bracing / Rogue boots / parkour still behave as vanilla when the chute isn't used.

### Phase 6: assets
- [ ] Unity 2022.3.62f2 project. Model, materials and prefab (§5.14 A/B). Icon render (C). `make_deploy_sound.py` → WAV (D). Bundle build (E).
- [ ] `ParachuteAssets` loads the prefab and clip, with fallbacks and clear errors if missing.
- [ ] `ParachuteVisual` animation (§5.13). `sounds.xml` node plus the deploy and flutter loop.
- **Accept:**
  - The canopy is visible in first person (look up) and third person, opens with an overshoot, banks on turns and collapses on stow.
  - No pink shaders, no collisions with the canopy.
  - The deploy sound plays once per deploy and the flutter loop stops on stow.
  - The icon shows in inventory, workbench, schematic and loot.

### Phase 7: polish & SP release (v0.9)
- [ ] Tune defaults, write a README/changelog, test the matrix below, bump to 0.9.0.

### Phase 8: multiplayer (v1.0), §6.

### Phase 9: worn parachute backpack (v1.0), §14.

---

## 9. Test plan (manual; run every item before each release)

| # | Scenario | Expected |
|---|---|---|
| T1 | Mod in head/hands/feet armor | Cannot install |
| T2 | Mod in chest outfit, on ground, double-tap Jump | Normal double jump; no deploy |
| T3 | Step off a 2 m ledge, double-tap | No deploy (below `min_deploy_height`) |
| T4 | `chute up 80`, double-tap after 1 s | Deploy sound; canopy opens; brakes to 4 m/s in ~0.8 s |
| T5 | Hold W / S / A / D under canopy | Faster dive / flare / yaw left / yaw right |
| T6 | Land on flat ground, on a block edge, on a steep slope, on a vehicle roof | 0 damage, no injury buffs; canopy collapses |
| T7 | Deploy ~4 m above ground while still braking | 0 damage (grace + MaxHeight reset) |
| T8 | Cutaway at 25 m | Vanilla 25 m fall damage |
| T9 | Fly into water under canopy | Auto-stow; normal swim |
| T10 | Grab a ladder under canopy | Auto-stow |
| T11 | Remove the chest armor / mod mid-air | Auto-stow; vanilla fall rules from that height |
| T12 | Die in air / respawn / teleport with canopy open | Canopy and loop cleaned up; no stuck state |
| T13 | Inventory open while falling, double-tap | No deploy |
| T14 | `chute reload` after changing `fall_speed` to 6 | New speed applies immediately |
| T15 | Coexist with GrapplingHook: grapple while under canopy | No auto-stow: the canopy stays open while grappling and the two mods interact freely. No exceptions; no stuck state after release |
| T19 | Visit traders at mid trader stage over several restocks | Mod sometimes in stock at 2500 (before bartering perks); schematic sometimes in stock. **Untested as of v0.6.0, see the note below** |
| T16 | Exit to menu and reload the world mid-flight | No exceptions; state stowed on load |
| T17 | Loot T2 schematic containers (`lootgroup schematicsModsAndGeneralT2`) at mid game stage | Schematic appears at a rate similar to Impact Bracing. **Untested, see the note below** |
| T18 | Phase 2: remote player sees canopy and banking; hears deploy; late-joiner sees an existing open canopy | ✓ |
| T20 | v1.0 backpack (§14): install the mod in worn chest armor, view in third person and the character screen; remove it; deploy and stow | Pack appears on the back while the mod is installed and disappears when it is removed; straps and lines run from the pack when the canopy is open |

**World loot (T17) is shipped untested.** `Config/loot.xml` adds the schematic to the vanilla `schematicsModsAndGeneralT2` loot group, the same pool as the Impact Bracing schematic, so it can turn up wherever tier-2 mod schematics drop. The drop is random and tied to loot stage, so like the trader stock it can only be confirmed over a long playthrough with the mods installed. Unverified until then: that the schematic drops at all, and that it is about as common as Impact Bracing's.

**Trader stock (T19) is shipped untested.** Added in v0.6.0 (`Config/traders.xml`, and `EconomicValue` 833.34 on the mod). It cannot be checked in a quick session: the stock is random and gated by trader stage, so it needs a long playthrough with the mods installed, across several trader restocks. Until Jason has done that, treat as unverified: that the mod and schematic appear in stock at all, that the mod's price is 2500, and that the XML patch loads without an XPath warning in the log.

---

## 10. Risks & mitigations

| Risk | Mitigation |
|---|---|
| A v3.3 update renames or changes `vp_FPController` methods | Patches use `nameof`-style strings in one file. Log a loud error and disable flight (fail safe) if any target method is missing |
| Fall damage still triggers on an edge-case landing frame | Three-layer guard (§5.4) plus grace window. T6/T7 tests |
| Canopy blocks the player's view | `canopy_height` and `canopy_scale` in config. Mostly above the camera |
| Bundle shader incompatibility | §5.14 fallback shader. Test early in Phase 6 with a cube before modelling |
| Sound bundle path in XML not resolved | C# playback fallback (§5.11) |
| `ParachuteWearing` vanilla flag misunderstood | Not used for logic (§3.4) |
| Double-tap conflicts with other mods using air-jump | Config `deploy_input = key` with `deploy_key` |
| `m_SpeedModifier` ≈ 0 (encumbrance/stun) causes a divide-by-small | Clamp ≥ 0.01, and cap the throttle magnitude |

---

## 11. Handoff notes for the next implementer
- Work file-by-file in the §8 order. **Don't** write C# before Phase 1's XML loads cleanly.
- When a **[VERIFY]** item is resolved, edit this plan in place: replace the tag with **[VERIFIED]** and the file:line.
- Grep the decompiled source at `D:\dotPeek Test\Assembly-CSharp\` for exact signatures before calling any game API. Line numbers here are approximate (±20).
- Keep every tunable in `ParachuteConfig.json`. No magic numbers in code other than the `60f` unit factor (§3.1), which gets a named constant `VpStepsPerSecondFactor = 60f` with a comment.
- All log lines start with `[Parachute]`.

## 12. Open questions for Jason (answer before or during Phase 7)
1. **Reusable vs consumable:** ~~keep it reusable forever (current plan), or consume it / add durability / a repack cooldown?~~ **Decided: reusable forever.**
2. **Cutaway:** keep "double-tap again = cut away", or only auto-stow on landing?
3. **Recipe balance:** ~~OK with 80 cloth, 10 leather, 3 duct tape, 1 sewing kit, 4 mechanical parts at the workbench?~~ **Decided: yes.**
4. **Trader stock:** ~~also sell the mod/schematic at traders (`modsTier2`), or loot only?~~ **Decided: traders randomly stock it, mod priced at 2500 (`Config/traders.xml`, EconomicValue 833.34). Not yet tested in game; needs a long playthrough (see T19).**
5. **GrapplingHook interaction:** ~~auto-stow when a grapple attaches?~~ **Decided: no auto-stow. The canopy stays open while grappling; the two mods do not know about each other.**
6. **Canopy style:** rectangular ram-air (planned) or a round military canopy? Any color preference?
7. **Test outfit:** `armorScavengerOutfit` Q6 OK as the default for `chute outfit`/`kit`?

## 13. GrapplingHook interaction (deferred; revisit later)

**Decision (2026-10-12):** the canopy does **not** auto-stow when a grapple attaches, and neither mod knows about the other. GrapplingHook is optional and may not be installed. The points below come from reading both mods' code side by side; none has been tested in game. Nothing here should crash or leave a stuck state.

| # | Behavior with both mods active | Where a fix would go |
|---|---|---|
| G1 | **Open canopy acts like a held Forward key on the rope.** `Patch_vpFPController.ThrottlePostfix` writes the glide into `m_MotorThrottle` every step; `Grapple.BeforeMove` reads `m_MotorThrottle` as swing input. Result: a constant 6 m/s² push in the facing direction and a quarter of the usual swing damping, so a swing can be pumped to the grapple's 30 m/s cap with no keys held. Fun "sail", or an exploit | **GrapplingHook** (preferred): ignore the throttle as input while the player has `buffParachuteDeployed`. No dependency between the mods |
| G2 | **No canopy braking on the rope.** The grapple integrates its own velocity from `m_GravityForce` and zeroes `m_FallSpeed`, so the descent limit is discarded while attached | **GrapplingHook** only: add drag while `buffParachuteDeployed` is present |
| G3 | **Jump does two jobs.** Jump releases the rope; double-tap Jump toggles the canopy. A double-tap to let go with the canopy open also cuts away. The canopy cannot be opened while staying on the rope (first tap releases); firing the grapple while already under canopy works. Release-and-deploy in one double-tap works | Either: a separate release key in GrapplingHook, or `deploy_input = key` here (already exists). Possibly nothing |
| G4 | **Swing momentum is lost under canopy.** `Grapple.Detach` hands momentum to the controller, then `ThrottlePostfix` overwrites x/z with the glide speed on the next step. Upward speed is kept. The same snap happens on any deploy while moving fast, grapple or not | **Parachute** only: start `CurrentGlideSpeed`/strafe from the controller's current horizontal velocity and approach the target, instead of overwriting |
| G5 | **No fall damage on any landing with the canopy open**, including off a fast swing. Existing parachute rule, now easier to reach | Parachute, if it should ever depend on speed |
| G6 | **Looks:** the canopy stays inflated directly overhead when hanging still, and has no collision, so it clips through the anchor block or an overhang | Parachute visual, cosmetic |

Order of calls each controller step, for whoever picks this up: `UpdateMotor` → `UpdateThrottleWalk` (parachute postfix) → `UpdateForces` (parachute postfix) → `FixedMove` (grapple prefix `BeforeMove`, then postfix `AfterMove`). The grapple therefore always sees the parachute's values and has the last word while attached.

G1 and G4 are the two worth deciding first: G1 for balance, G4 because it works against swing-into-glide moves.

## 14. Worn parachute backpack (v1.0 feature; requested 2026-10-12, not started)

**Goal:** while `modArmorParachute` is installed in the chest armor the player is wearing, a parachute backpack is visible on the character's back. Today the mod has no worn visual at all: the canopy, lines and riser straps only exist while the canopy is open.

Notes for whoever builds it (none of this is verified in game):
- **When to show it:** the same condition the deploy check uses, `buffParachuteHarness` on the player, which the item modifier adds and removes on equip.
- **Where to attach it:** an upper-spine bone from `EModelSDCS.boneCatalog` (the catalog already used for the shoulder bones in `ParachuteVisual`; `Spine3` exists on this rig per `AvatarSDCSController`). Parent the pack to the bone so it follows the body's animation. The offset and rotation will need tuning by eye, and may differ between body types and bulky chest armors.
- **The model:** a new prefab in the Unity project, built by `CanopyBuilder` or a sibling script, with no colliders or rigidbodies. The icon's pack (`IconRenderer.BuildPack`) is a rough stand-in made of primitives and is not good enough to wear; this needs a proper pack mesh with shoulder straps.
- **First person:** the body is not drawn, so nothing to show. Check the pack never appears in front of the camera.
- **Other views:** the character preview in the inventory screen uses its own copy of the player model, so the pack will not show there unless it is attached to that copy as well. Decide whether that matters.
- **Tie-in with the canopy:** once the pack exists, the riser straps should rise from the pack's shoulder straps, and the deploy animation can start the canopy at the pack instead of overhead.
- **Multiplayer:** other players should see the pack. That makes this depend on the Phase 8 work in §6: a remote player's equipped armor and its mods are already replicated by the game, so a client can decide locally whether to show the pack on someone else, without a new network message.
- **Clipping:** the game's own backpack-style visuals and long coats may overlap it; accept or special-case.