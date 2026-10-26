# Parachute

A steerable ram-air parachute for **7 Days to Die v3.4 (b6)**, packed into your chest armor as an armor mod. Jump off something tall, press Jump in the air, and glide down with no fall damage. Works in single player, peer-hosted games and on dedicated servers.

## Features

- **One press to open:** while falling, press Jump to open the canopy. Press Jump again to cut it away.
- **Steerable:** hold Forward to dive faster, Back to flare and slow down, Left and Right to slide sideways. The canopy glides the way you look.
- **No fall damage** while the canopy is open, including the touchdown.
- **Reusable:** the parachute is never used up. It repacks itself after every landing.
- **Third-person view under the canopy:** the camera pulls back behind you like a vehicle camera, then returns to your chosen view when you land.
- **Worn pack:** a parachute pack shows on your back while the mod is installed, and the riser straps run from it when the canopy opens.
- **Multiplayer:** other players see your canopy open, steer and collapse, hear it from where you are, and see your pack.
- **Configurable:** speeds, controls, camera and pack placement are all in `ParachuteConfig.json`.

## Getting one

- **Craft it** at a workbench once you know the recipe: 80 cloth, 10 leather, 3 duct tape, 1 sewing kit and 4 mechanical parts.
- **Learn the recipe** from the *Parachute Mod Schematic*, found as loot with the other tier-2 mod schematics or bought from a trader.
- **Buy it** from traders, who sometimes stock it with their other armor mods.

Install the Parachute Mod in the **chest armor** you are wearing.

## Controls

| Input | Under the canopy |
|---|---|
| Jump (in the air) | Open the canopy; press again to cut away |
| Forward | Dive: faster forward and down |
| Back | Flare: slower forward and down |
| Left / Right | Slide sideways |
| Mouse | Steer |

- The canopy needs at least 3 m of clear air below you to open. The Jump that leaves the ground never opens it.
- It won't open in water, on a ladder, in a vehicle or while wearing a jetpack.
- It stows itself when you land, enter water, grab a ladder or get in a vehicle.

## Installation

1. Unzip into the game's `Mods` folder so you have `Mods\Parachute\ModInfo.xml`.
2. Keep `Mods\0_TFP_Harmony`, which comes with the game.
3. Start the game with **Easy Anti-Cheat off**.

### Multiplayer

- Install the mod on the **server and every player's game**.
- **Dedicated server:** put it in the server's `Mods` folder and set `EACEnabled` to `false` in `serverconfig.xml`.
- **Peer-hosted:** the host and every player who joins install it and play with EAC off.
- A player who joins without the mod isn't kicked, but can't open a canopy and won't see anyone else's.
- Each player's `ParachuteConfig.json` applies only to their own game.

## Compatibility

- Made for **v3.4 (b6)**. For v3.3 use Parachute 0.6.4.
- Works alongside the Grappling Hook mod. The Jump press that lets go of a rope doesn't open the canopy.
- Uses Harmony patches on player movement, fall damage and the camera, so other mods that change those may conflict.

## Known issues

- One pack placement doesn't fit every armor: with some outfits the straps sink into the clothing, and armor with its own backpack can clash with the pack.
- Other players' canopies lean and tilt from an estimate of how they move, so it can lag slightly behind what they see.
- Another player's canopy collapses about half a second after they land.
- The deploy sound will be revisited.

## Version

1.0.0-beta (the game shows 1.0.0).
