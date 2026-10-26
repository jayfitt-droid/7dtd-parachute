# Parachute for 7 Days to Die v3.4 (b6)

A mod that adds a parachute as a chest-armor mod, for single player and multiplayer (peer-hosted games and dedicated servers). Install it in the chest armor you are wearing, and while falling you can press Jump to open a steerable ram-air canopy. You take no fall damage while the canopy is open. The parachute is not used up: it repacks itself and can be deployed again as often as you like.

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

### Multiplayer

Install the same version of the mod on the server and on every player's game.

- **Dedicated server:** unzip it into the server's `Mods` folder, next to `0_TFP_Harmony`, and turn Easy Anti-Cheat off in `serverconfig.xml` (`EACEnabled` set to `false`).
- **Peer-hosted game:** the host and everyone who joins installs it and starts the game with Easy Anti-Cheat off.
- **Why every player needs it:** the server sends its item, recipe, buff and sound settings to players as they join, but not the mod's code, the canopy and pack models or the inventory icon. A player who joins without the mod is not kicked, but cannot open a canopy, sees a blank icon for the parachute items, and does not see other players' canopies or packs.
- The mod adds no network messages of its own. Each player's game flies that player's canopy and decides their fall damage; other players' games draw the canopy, lines, pack and sounds from the "canopy open" state the game already shares between players.
- Settings in `ParachuteConfig.json` are read by each player's own game and are not sent by the server. Speeds and the camera follow your own file. To keep everyone the same, give all players the same file.
- `chute` console commands need admin permission on a server, like the game's own cheat commands.

## Controls

The parachute uses the game's jump and movement actions, so it follows whatever you have bound in the game's controls.

| Input | Under the canopy |
|---|---|
| Jump while in the air | Open the canopy; press again to cut it away |
| Forward | Dive: faster forward and a faster descent |
| Back | Flare: slower forward and a slower descent |
| Left / Right | Slide sideways; you keep facing where you look |
| Mouse | Steer: the canopy glides in the direction you face |

- Only a press made in the air counts. The press that starts a jump from the ground does not, so jump once to leave the ground and press Jump again to open.
- For a quarter of a second after the canopy opens, Jump does not cut it away, so a quick second press will not close it again.
- The canopy needs at least 3 m of clear air below your feet to open. If it refuses to open, a tooltip says why.
- It will not open in water, on a ladder, in a vehicle, in fly or god mode, while wearing a jetpack, or while a window is open.
- The canopy stows by itself when you land, enter water, grab a ladder, get in a vehicle, switch to fly or god mode, remove the chest armor or the mod, die or teleport.
- After a cutaway you fall normally from that point and take normal fall damage.
- You can keep a weapon or tool in hand and use it under the canopy.
- Half a second after the canopy opens, the camera switches to third person, the way it does when you get into a vehicle. When the canopy stows it goes back to the view you last chose with the view key. If you change the view yourself while the canopy is open, that choice is kept. Turn this off with `auto_third_person`.
- In third person under the canopy the camera sits behind you like a vehicle camera, further back the faster you glide, and your character keeps facing where the camera looks while you slide sideways or flare. Aiming still uses the game's close over-the-shoulder view. Turn the vehicle-style camera off with `chase_camera`.
- The canopy, its lines and the two riser straps on your shoulders are visible in first person (look up) and in third person.
- While the mod is installed in the chest armor you are wearing, a parachute pack sits on your back. You see it in third person and on the character in the inventory screen, and other players who have the mod see it on you. Under the canopy, the riser straps start at the top of the pack's shoulder straps. Turn the pack off with `show_pack`, move it with `pack_offset`, `pack_rotation` and `pack_scale`, or fit the front straps to your armor with `pack_chest_depth`.

## Configuration

Edit `Mods\Parachute\Config\ParachuteConfig.json`, then restart the game or run `chute reload` in the console. Every setting has a comment in the file describing it and its allowed range; an invalid value falls back to its default and the game log says so. Speeds are in meters per second.

| Setting | Default | Meaning |
|---|---:|---|
| `deploy_input` | `jump` | How the canopy is opened: `jump`, `key` or `both` |
| `deploy_key` | (off) | An optional dedicated key, as a Unity key name such as `G` |
| `cutaway_lockout_ms` | 250 | After opening, how long Jump cannot cut the canopy away |
| `auto_third_person` | true | Switch to third person under the canopy and back when it stows |
| `third_person_after_ms` | 500 | How long after opening the switch happens; 0 is at once |
| `chase_camera` | true | Vehicle-style camera in third person under the canopy |
| `chase_camera_height` | 1.8 | Height above your feet the chase camera looks at |
| `chase_camera_distance_min` | 4 | Chase camera distance when hanging still |
| `chase_camera_distance_max` | 6 | Chase camera distance at full forward speed |
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
| `allow_cutaway` | true | Whether pressing Jump again cuts the canopy away |
| `redeploy_cooldown_ms` | 300 | Wait after stowing before it can open again |
| `landing_grace_ms` | 500 | Fall-damage protection kept just after touchdown |
| `canopy_scale` | 1.0 | Visual size of the canopy |
| `canopy_height` | 3.2 | Height of the canopy above your feet, in meters |
| `show_pack` | true | Show the parachute pack on your back while the mod is installed |
| `pack_offset` | [0, 0.06, -0.14] | Pack position from the upper spine: right, up, out of the chest, in meters |
| `pack_rotation` | [0, 0, 0] | Extra pack rotation in those axes, in degrees |
| `pack_scale` | 1.0 | Size of the pack |
| `pack_chest_depth` | 0.92 | How far the straps come out in front, to fit bulkier or slimmer chest armor |
| `play_flutter_loop` | true | Play the flapping sound while the canopy is open |
| `flutter_pitch` | 0.7 | Pitch of that sound; 1.0 is the game's own, lower is deeper |
| `debug` | false | Extra logging and an on-screen speed readout |

## Console commands

For testing. The command is `chute`, or `parachute`.

- `chute give` adds a chest outfit, the schematic and the mod to the backpack. `chute outfit [item] [quality]`, `chute mod [count]` and `chute schematic [count]` give them one at a time, and `chute kit` gives an outfit with the mod already installed.
- `chute up [meters]` lifts you straight up (60 m by default) for a drop test.
- `chute deploy` and `chute stow` open and close the canopy without pressing Jump.
- `chute status` prints the current state and settings, `chute reload` re-reads the config file, and `chute debug [on|off]` switches the readout.
- The game's own `sv` command switches between first and third person, which is the easy way to look at the canopy.

Mod messages in the game log start with `[Parachute]`.

## With the Grappling Hook mod

The two mods work side by side and neither needs the other. The Jump press that lets go of a rope never opens or cuts away the canopy. The canopy stays open while you are on a rope, which leads to some untested interactions: an open canopy pushes a swing along as if Forward were held, and it does not slow you while the rope is attached. These are written up in section 13 of `plan.md`.

## Building from source

Requirements: the .NET SDK and a 7 Days to Die v3.4 install. The project file and `tools\deploy.ps1` point at `C:\7D2D\Custom\v3.4`; change those paths to your own install, or pass `-p:GamePath=...` to the build.

- `powershell -ExecutionPolicy Bypass -File package.ps1` compiles the mod and writes `dist\Parachute-<version>.zip`, the file players install. The version comes from `Mod\Parachute\ModInfo.xml`. It uses the asset bundle and icon as they are, so rebuild those first if the art changed.
- For development, `build-deploy.cmd` compiles the DLL into `Mod\Parachute` and mirrors that folder into the game's `Mods\Parachute`. It stops before deploying if the build fails. Only `Mods\Parachute` is touched.
- The art lives in the Unity project `unity\ParachuteAssets` and is generated by code, so there are no hand-made models. Unity **2022.3.62f2** builds it; the version must match the game's engine, because bundles from a newer editor do not load.
  - `ParachuteEditor.BuildBundles.Build` builds the canopy, lines, straps, worn pack and deploy sound into `Mod\Parachute\Resources\Parachute.unity3d`.
  - `ParachuteEditor.CanopyPreview.Render` renders the canopy to PNG files for a quick look without starting the game.
  - `ParachuteEditor.IconRenderer.Render` renders the inventory icon's master image; `tools\make_icon.py` trims and shrinks it to the 160x160 icon.
- `tools\make_deploy_sound.py` generates the deploy sound (needs Python with numpy).
- `release\description.md` and `release\description.bbcode` are the mod page descriptions, in Markdown and in BBCode for sites such as Nexus Mods. Update both with each release.
- `plan.md` is the design document: how the flight and fall-damage patches work, the test plan, and what is still to do.

## Status

Version 1.0.0-beta (the game shows 1.0.0). Built against v3.4 (b6). Deploying, steering, the canopy, the pack and the sounds have been tried in single player, and multiplayer has been tried on a dedicated server with two players. Not yet tried: a peer-hosted game, late joining and some of the other cases in the multiplayer test plan (`plan.md` section 9). The deploy sound will be revisited. The trader stock and the schematic's world-loot drop have not been seen in game yet, because they need a long playthrough. See `CHANGELOG.md` for what changed in each version.
