# Changelog

All notable changes to the Parachute mod. Versions match the Git tags and the version in `Mod\Parachute\ModInfo.xml`, except that a pre-release tag such as `v1.0.0-beta` carries the plain version (`1.0.0`) in `ModInfo.xml`, which is what the game shows.

## 1.0.0-beta - 2026-10-26

`ModInfo.xml` says 1.0.0. Multiplayer was tested on a v3.4 dedicated server with two players on separate PCs: each saw the other's canopy open, steer and collapse, the hanging pose, the pack and the sounds, with no errors in the logs. Not yet tested: a peer-hosted game, late joining, and removing the mod while another player watches.

### Changed
- Built for 7 Days to Die v3.4 (b6). On v3.3, keep using 0.6.4.

### Added
- Multiplayer, in peer-hosted games and on dedicated servers. Other players with the mod now see your canopy, its lines and riser straps open, bank and collapse, and hear the deploy sound and the flutter loop from where you are. Players who join or come into range while your canopy is open see it within a few seconds. The mod sends no network messages of its own, so a player without it is never kicked for that; they just do not see the parachute. Install the mod on the server and on every player's game (see "Multiplayer" in `README.md`).
- `chute status` lists the other players this game shows under a canopy.
- Other players under a canopy hang in the idle pose instead of walking in the air. Each game works out other players' walk animation from their speed, so the mod now applies the hanging pose there too.

### Fixed
- Another player's worn pack now appears and disappears when they install or remove the parachute in the chest armor they are already wearing, instead of waiting for their next equipment change.
- The collapsing canopy could stay hanging over the player after a stow when the frame rate was below the physics rate. The canopy now finishes collapsing on its own clock.
- On a Windows dedicated server the mod could not find its folder, logged "Config load failed: Invalid path" and ignored `ParachuteConfig.json`. It now uses the folder the game gives it.
- The version written to the game log on load was out of date.
- The deploy sound ended in a loud low thump about a second after opening. The bass "inflation thump" is gone; a light fabric snap is left, and the rip and whoosh are as loud as before.
- `debug` was shipped switched on since 0.6.3, showing a speed readout on screen. It is off again by default.

### Known issues
- Under another player's canopy, the banking and dive/flare pitch are estimated from how they move and judged against your own speed settings, so they can lag slightly or differ from what that player sees.
- Another player's canopy collapses about half a second after they land, when their landing grace ends.
- The deploy sound is better without the thump but is not final; it will be revisited.

## 0.6.4 - 2026-10-16

### Added
- A parachute pack on your back while the mod is installed in the chest armor you are wearing: a sport-rig container with the reserve on top, shoulder straps down to the waist, hip straps round to the container, a chest strap and a pilot-chute handle. It shows in third person, on the inventory character preview and on other players who also have the mod. It is hidden in first person, where the body is not drawn.
- Under the canopy, the riser straps now start at the top of the pack's shoulder straps instead of at the shoulders.
- New settings `show_pack` (true), `pack_offset` ([0, 0.06, -0.14]), `pack_rotation` ([0, 0, 0]), `pack_scale` (1.0) and `pack_chest_depth` (0.92), which moves the front of the straps out from or in toward the chest to fit the armor. `chute reload` moves the pack live.

### Known issues
- One placement does not fit every armor. With the Preacher outfit the bottom of the straps and the hip straps sink into the sash, and armor that has its own backpack clashes with the parachute pack.

## 0.6.3 - 2026-10-16

### Added
- The camera switches to third person 0.5 s after the canopy opens, as in the Glider Suit mod, and goes back to the view last chosen with the view key when the canopy stows. A view picked with the view key while the canopy is open is kept. Skipped if the world restricts the camera. New settings `auto_third_person` (true) and `third_person_after_ms` (500).
- A vehicle-style camera in third person under the canopy, as in the Glider Suit mod: centred behind you, 4 to 6 m back depending on forward speed. Aiming keeps the game's own view. New settings `chase_camera` (true), `chase_camera_height` (1.8), `chase_camera_distance_min` (4) and `chase_camera_distance_max` (6).

### Fixed
- In third person under the canopy the character no longer turns to face sideways or back at the camera when strafing or flaring; it faces where the camera looks, and the canopy glides that way. `left_right` `turn` mode now turns the view in third person too.
- Aiming down sights while the canopy stowed left the zoom stuck on. The switch back to first person, and the switch to third person after opening, now wait until you stop aiming.

## 0.6.2 - 2026-10-16

### Changed
- One Jump press in the air now opens the canopy, as in the Glider Suit mod, instead of a double-tap. The press that starts a jump from the ground never counts. Pressing Jump again cuts the canopy away.
- `deploy_input` is now `jump`, `key` or `both`. The old value `double_tap_jump` still works and is read as `jump`.
- `double_tap_window_ms` is gone. New `cutaway_lockout_ms` (250): for that long after opening, Jump does not cut the canopy away.

### Fixed
- The Jump press that lets go of a Grappling Hook rope no longer opens or cuts away the canopy.
- Pressing Jump in the air without the mod installed no longer logs a refusal.

## 0.6.1 - 2026-10-12

### Added
- `README.md` and this changelog.
- `package.ps1`, which builds the mod and packs `dist\Parachute-<version>.zip` for players.

### Fixed
- The mod's in-game description said Left and Right turn you; it now says they slide you sideways, which has been the default since 0.4.0.

## 0.6.0 - 2026-10-12

### Added
- Traders can stock the Parachute Mod at random with their other armor mods, priced at 2500 before bartering perks, and the schematic with their other mod schematics. Not yet seen in game: it needs a long playthrough.

### Changed
- The mod's economic value went from 900 to 833.34 to give the 2500 trader price, so selling one to a trader pays slightly less than before.

### Notes
- Decided that the parachute stays reusable, that the recipe stays as it is, and that the canopy does not stow when the separate Grappling Hook mod attaches a rope. The interactions between the two mods are recorded in `plan.md`, section 13.

## 0.5.0 - 2026-10-12

### Added
- Riser straps: a webbing strap rises about 45 cm from each shoulder, and the suspension lines on that side gather at a link on top of it.

### Changed
- The lines no longer all converge on the shoulders.

## 0.4.0 - 2026-10-12

First version under version control.

### Added
- The Parachute Mod, an armor mod for chest armor, and its schematic. Workbench recipe: 80 cloth, 10 leather, 3 duct tape, 1 sewing kit, 4 mechanical parts. The schematic is in the tier-2 mod-schematic loot group.
- Double-tap Jump in the air to open the canopy, and again to cut it away. It needs 3 m of clear air below and refuses with an on-screen reason when it cannot open.
- Flight: a 4 m/s descent and 4 m/s forward glide; Forward dives (5.5 down, 6 forward), Back flares (2.75 down, 1.5 forward).
- Left and Right slide sideways at 3 m/s; the old turning behaviour is available with `left_right` set to `turn`.
- No fall damage while the canopy is open, or for half a second after touchdown.
- The canopy stows by itself on landing, in water, on a ladder, in a vehicle, in fly or god mode, when the mod or armor is removed, on death and on teleport.
- A seven-cell ram-air canopy with a fabric texture, open cell inlets, wing-tip stabilizers and 32 dark rope lines. In third person the lines follow the character's shoulder bones.
- Under the canopy the character holds its idle pose instead of the falling animation, and can keep using a weapon.
- A generated deploy sound and a deepened version of the game's flutter loop (`flutter_pitch`).
- An inventory icon rendered in 3D to match the game's own item icons.
- `ParachuteConfig.json` with every tunable, and the `chute` console commands for testing.
