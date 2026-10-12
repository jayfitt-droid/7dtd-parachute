# Changelog

All notable changes to the Parachute mod. Versions match the Git tags and the version in `Mod\Parachute\ModInfo.xml`.

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
