using System.Reflection;
using HarmonyLib;

/// <summary>
/// Attaches the worn pack whenever the game rebuilds a character's meshes: the
/// world (third-person) rig and the inventory preview rig. Ported from the
/// GliderSuit mod. Each class checks its target in Prepare, so a game update
/// that renames one only loses the pack instead of failing the patch, which
/// would disable deploying.
/// </summary>
[HarmonyPatch(typeof(SDCSUtils), "CreateVizTP")]
internal static class Patch_SDCSCreateVizTP
{
    [HarmonyPrepare]
    private static bool Prepare()
    {
        if (AccessTools.Method(typeof(SDCSUtils), "CreateVizTP") != null) return true;
        Log.Error("[Parachute] SDCSUtils.CreateVizTP was not found; the worn pack is disabled.");
        return false;
    }

    /// <summary>CreateVizTP(Archetype, ref GameObject baseRig, ref TransformCatalog boneCatalog, EntityAlive entity, bool isFPV)</summary>
    [HarmonyPostfix]
    private static void Postfix(SDCSUtils.TransformCatalog __2, EntityAlive __3)
    {
        try
        {
            ParachuteWorn.Sync(__3, __2, WornKind.World);
        }
        catch (System.Exception ex)
        {
            Log.Warning("[Parachute] Worn pack error: " + ex.Message);
        }
    }
}

/// <summary>
/// The 5-argument CreateVizUI overload used by the inventory character windows.
/// The 3-argument one is the character creator and is deliberately left alone.
/// Its signature contains ref parameters, which cannot go in an attribute, so
/// the target is resolved at patch time.
/// </summary>
[HarmonyPatch]
internal static class Patch_SDCSCreateVizUI
{
    [HarmonyPrepare]
    private static bool Prepare()
    {
        if (Target() != null) return true;
        Log.Error("[Parachute] SDCSUtils.CreateVizUI (5 arguments) was not found; the inventory preview pack is disabled.");
        return false;
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return Target();
    }

    /// <summary>
    /// CreateVizUI(Archetype, ref GameObject baseRigUI, ref TransformCatalog boneCatalogUI,
    /// EntityAlive entity, bool useTempCosmetics). Matched by shape rather than by an
    /// exact by-ref signature.
    /// </summary>
    private static MethodBase Target()
    {
        foreach (MethodBase method in AccessTools.GetDeclaredMethods(typeof(SDCSUtils)))
        {
            if (method.Name != "CreateVizUI") continue;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 5 && parameters[0].ParameterType == typeof(Archetype))
                return method;
        }
        return null;
    }

    [HarmonyPostfix]
    private static void Postfix(SDCSUtils.TransformCatalog __2, EntityAlive __3)
    {
        try
        {
            ParachuteWorn.Sync(__3, __2, WornKind.Ui);
        }
        catch (System.Exception ex)
        {
            Log.Warning("[Parachute] Preview pack error: " + ex.Message);
        }
    }
}
