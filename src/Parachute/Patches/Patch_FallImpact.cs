using HarmonyLib;

/// <summary>
/// Primary fall-damage immunity: skip the whole vanilla impact (cvar, buff
/// event and hard-landing sound) while the canopy is open or within the landing
/// grace window.
/// </summary>
[HarmonyPatch(typeof(EntityPlayerLocal), "FallImpact")]
internal static class Patch_FallImpact
{
    private static bool Prefix(EntityPlayerLocal __instance)
    {
        if (ParachuteState.Player == null || __instance != ParachuteState.Player) return true;
        return !(ParachuteState.IsActive || ParachuteState.InGrace);
    }
}
