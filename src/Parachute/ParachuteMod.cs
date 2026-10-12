using System;
using System.IO;
using System.Reflection;
using HarmonyLib;

public sealed class ModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        // Each step is isolated so one failure cannot silently take the rest of
        // the mod down with it. Events come first; patches last.
        try
        {
            ParachuteMod.Directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            ParachuteMod.Reload();
        }
        catch (Exception ex)
        {
            Log.Error("[Parachute] Config load failed; using defaults: " + ex);
        }

        try
        {
            ModEvents.PlayerSpawnedInWorld.RegisterHandler(ParachuteMod.OnPlayerSpawnedInWorld);
            ModEvents.WorldShuttingDown.RegisterHandler(ParachuteMod.OnWorldShuttingDown);
        }
        catch (Exception ex)
        {
            Log.Error("[Parachute] Event registration failed: " + ex);
        }

        ParachuteMod.ApplyPatches();
        Log.Out("[Parachute] Loaded v" + ParachuteMod.Version + ".");
    }
}

internal static class ParachuteMod
{
    internal const string Version = "0.6.1";
    internal static string Directory = "";
    internal static ParachuteConfig Settings = new ParachuteConfig();

    /// <summary>
    /// False if any Harmony patch failed to apply. Deploying is then refused, so
    /// a half-patched game can never give a canopy without its braking or its
    /// fall-damage protection.
    /// </summary>
    internal static bool PatchesOk = true;

    internal static void ApplyPatches()
    {
        var harmony = new Harmony("jason.parachute");
        int applied = 0;
        int failed = 0;
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (!type.IsDefined(typeof(HarmonyPatch), false)) continue;
            try
            {
                harmony.CreateClassProcessor(type).Patch();
                applied++;
            }
            catch (Exception ex)
            {
                failed++;
                Log.Error("[Parachute] PATCH FAILED: " + type.Name + ": " + ex);
            }
        }

        PatchesOk = failed == 0;
        if (PatchesOk)
            Log.Out("[Parachute] " + applied + " patch classes applied.");
        else
            Log.Error("[Parachute] " + failed + " patch class(es) failed, " + applied +
                " applied. Deploying is DISABLED until this is fixed.");
    }

    internal static void Reload()
    {
        Settings = ParachuteConfig.Load(Directory);
    }

    internal static void OnPlayerSpawnedInWorld(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        if (!data.IsLocalPlayer) return;
        try
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            EntityPlayerLocal player = world != null ? world.GetEntity(data.EntityId) as EntityPlayerLocal : null;
            if (player == null && world != null) player = world.GetPrimaryPlayer();
            if (player == null) return;
            ParachuteController.Attach(player);
        }
        catch (Exception ex)
        {
            Log.Warning("[Parachute] PlayerSpawnedInWorld: " + ex.Message);
        }
    }

    internal static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        try
        {
            ParachuteController.Detach();
            ParachuteAssets.Clear();
        }
        catch (Exception ex)
        {
            Log.Warning("[Parachute] WorldShuttingDown: " + ex.Message);
        }
    }
}
