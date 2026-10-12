# Parachute for 7 Days to Die v3.3.0 (b18)

A single-player mod that adds a parachute as a chest-armor mod. Install it in the chest armor you are wearing, and while falling you can double-tap Jump to open a steerable ram-air canopy. You take no fall damage while the canopy is open. The parachute is not used up: it repacks itself and can be deployed again as often as you like.

| Item | What it is | How you get it |
|---|---|---|
| Parachute Mod (`modArmorParachute`) | Armor mod that fits chest armor only | Craft it at a workbench once you know the recipe, or buy it from a trader |
| Parachute Mod Schematic (`modArmorParachuteSchematic`) | Read it to learn the recipe | Found as loot with the other tier-2 mod schematics, or bought from a trader |

Recipe, at a workbench: 80 cloth, 10 leather, 3 duct tape, 1 sewing kit and 4 mechanical parts.

Traders can stock the mod at random with their other armor mods, for 2500 before any bartering perk, and the schematic with their other mod schematics.

## Install

1. Unzip `Parachute-<version>.zip` into the game's `Mods` folder. You should end up with `Mods\Parachute\ModInfo.xml`. The zip is made by `package.ps1` (see "Building from source").
2. Make sure `Mods\0_TFP_Harmony` is there. It ships with the game and loads the mod's code.
3. Start the game with Easy Anti-Cheat turned off; mods with code do not load with it on.

To remove the mod, delete `Mods\Parachute`.

## Controls

The parachute uses the game's jump and movement actions, so it follows whatever you have bound in the game's controls.

| Input | Under the canopy |
|---|---|
| Double-tap Jump while in the air | Open the canopy; double-tap again to cut it away |
| Forward | Dive: faster forward and a faster descent |
| Back | Flare: slower forward and a slower descent |
| Left / Right | Slide sideways; you keep facing where you look |
| Mouse | Steer: the canopy glides in the direction you face |

- Both taps of the double-tap must happen in the air. The tap that starts a jump from the ground does not count.
- The canopy needs at least 3 m of clear air below your feet to open. If it refuses to open, a tooltip says why.
- It will not open in water, on a ladder, in a vehicle, in fly or god mode, while wearing a jetpack, or while a window is open.
- The canopy stows by itself when you land, enter water, grab a ladder, get in a vehicle, switch to fly or god mode, remove the chest armor or the mod, die or teleport.
- After a cutaway you fall normally from that point and take normal fall damage.
- You can keep a weapon or tool in hand and use it under the canopy.
- The canopy, its lines and the two riser straps on your shoulders are visible in first person (look up) and in third person.

## Configuration

Edit `Mods\Parachute\Config\ParachuteConfig.json`, then restart the game or run `chute reload` in the console. Every setting has a comment in the file describing it and its allowed range; an invalid value falls back to its default and the game log says so. Speeds are in meters per second.

| Setting | Default | Meaning |
|---|---:|---|
| `deploy_input` | `double_tap_jump` | How the canopy is opened: `double_tap_jump`, `key` or `both` |
| `deploy_key` | (off) | An optional dedicated key, as a Unity key name such as `G` |
| `double_tap_window_ms` | 350 | Longest gap between the two taps |
| `min_deploy_height` | 3.0 | Clear air needed below your feet, in meters |
| `fall_speed` | 4.0 | Descent with no input |
| `fall_speed_forward` | 5.5 | Descent while holding Forward |
| `fall_speed_back` | 2.75 | Descent while holding Back |
| `glide_speed` | 4.0 | Forward speed with no input |
| `glide_speed_forward` | 6.0 | Forward speed while holding Forward |
| `glide_speed_back` | 1.5 | Forward speed while holding Back |
| `left_right` | `strafe` | What Left/Right do: `strafe` slides sideways, `turn` rotates you |
| `strafe_speed` | 3.0 | Sideways speed in strafe mode |
| `turn_rate` | 75 | Degrees per second in turn mode |
| `control_response` | 6.0 | How quickly speeds change, in m/s² |
| `deploy_time` | 0.8 | Seconds for the canopy to open and brake |
| `stow_anim_time` | 0.4 | Seconds for the canopy to collapse |
| `allow_cutaway` | true | Whether a second double-tap cuts the canopy away |
| `redeploy_cooldown_ms` | 300 | Wait after stowing before it can open again |
| `landing_grace_ms` | 500 | Fall-damage protection kept just after touchdown |
| `canopy_scale` | 1.0 | Visual size of the canopy |
| `canopy_height` | 3.2 | Height of the canopy above your feet, in meters |
| `play_flutter_loop` | true | Play the flapping sound while the canopy is open |
| `flutter_pitch` | 0.7 | Pitch of that sound; 1.0 is the game's own, lower is deeper |
| `debug` | false | Extra logging and an on-screen speed readout |

## Console commands

For testing. The command is `chute`, or `parachute`.

- `chute give` adds a chest outfit, the schematic and the mod to the backpack. `chute outfit [item] [quality]`, `chute mod [count]` and `chute schematic [count]` give them one at a time, and `chute kit` gives an outfit with the mod already installed.
- `chute up [meters]` lifts you straight up (60 m by default) for a drop test.
- `chute deploy` and `chute stow` open and close the canopy without the double-tap.
- `chute status` prints the current state and settings, `chute reload` re-reads the config file, and `chute debug [on|off]` switches the readout.
- The game's own `sv` command switches between first and third person, which is the easy way to look at the canopy.

Mod messages in the game log start with `[Parachute]`.

## With the Grappling Hook mod

The two mods work side by side and do not know about each other. The canopy stays open while you are on a rope, which leads to some untested interactions: an open canopy pushes a swing along as if Forward were held, it does not slow you while the rope is attached, and Jump both releases the rope and (double-tapped) toggles the canopy. These are written up in section 13 of `plan.md`.

## Building from source

Requirements: the .NET SDK and a 7 Days to Die v3.3 install. The project file and `tools\deploy.ps1` point at `C:\7D2D\Custom\v3.3`; change those paths to your own install, or pass `-p:GamePath=...` to the build.

- `powershell -ExecutionPolicy Bypass -File package.ps1` compiles the mod and writes `dist\Parachute-<version>.zip`, the file players install. The version comes from `Mod\Parachute\ModInfo.xml`. It uses the asset bundle and icon as they are, so rebuild those first if the art changed.
- For development, `build-deploy.cmd` compiles the DLL into `Mod\Parachute` and mirrors that folder into the game's `Mods\Parachute`. It stops before deploying if the build fails. Only `Mods\Parachute` is touched.
- The art lives in the Unity project `unity\ParachuteAssets` and is generated by code, so there are no hand-made models. Unity **2022.3.62f2** builds it; the version must match the game's engine, because bundles from a newer editor do not load.
  - `ParachuteEditor.BuildBundles.Build` builds the canopy, lines, straps and deploy sound into `Mod\Parachute\Resources\Parachute.unity3d`.
  - `ParachuteEditor.CanopyPreview.Render` renders the canopy to PNG files for a quick look without starting the game.
  - `ParachuteEditor.IconRenderer.Render` renders the inventory icon's master image; `tools\make_icon.py` trims and shrinks it to the 160x160 icon.
- `tools\make_deploy_sound.py` generates the deploy sound (needs Python with numpy).
- `plan.md` is the design document: how the flight and fall-damage patches work, the test plan, and what is still to do.

## Status

Version 0.6.1. Built against single-player v3.3.0 (b18) and still in development: deploying, steering, the canopy and the sounds have been tried in game, but the full test plan in `plan.md` has not been run. In particular, the trader stock and the schematic's world-loot drop have not been seen in game yet, because they need a long playthrough.

Multiplayer and dedicated servers are not supported: other players do not see your canopy. That, and a parachute backpack worn on the character, are planned for 1.0. See `CHANGELOG.md` for what changed in each version.
