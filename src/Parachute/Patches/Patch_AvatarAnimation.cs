using HarmonyLib;

/// <summary>
/// Keeps the body out of the flailing fall animation under an open canopy. The
/// game feeds the animator "in the air" and the walk/run speeds every frame;
/// while the canopy is deploying or open those are replaced with "standing
/// still", so the character hangs in its idle pose instead.
/// </summary>
[HarmonyPatch(typeof(AvatarLocalPlayerController))]
internal static class Patch_AvatarAnimation
{
    [HarmonyPrefix]
    [HarmonyPatch("_setBool", typeof(int), typeof(bool), typeof(bool))]
    private static void BoolPrefix(AvatarLocalPlayerController __instance, int _pid, ref bool _value)
    {
        if (!IsLocalActive(__instance)) return;
        if (_pid == AvatarController.inAirHash || _pid == AvatarController.isMovingHash) _value = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("_setFloat", typeof(int), typeof(float), typeof(bool))]
    private static void FloatPrefix(AvatarLocalPlayerController __instance, int _pid, ref float _value)
    {
        if (!IsLocalActive(__instance)) return;
        if (_pid == AvatarController.forwardHash || _pid == AvatarController.strafeHash) _value = 0f;
    }

    [HarmonyPrefix]
    [HarmonyPatch("_setInt", typeof(int), typeof(int), typeof(bool))]
    private static void IntPrefix(AvatarLocalPlayerController __instance, int _pid, ref int _value)
    {
        if (!IsLocalActive(__instance)) return;
        if (_pid == AvatarController.movementStateHash) _value = 0;
    }

    private static bool IsLocalActive(AvatarController controller)
    {
        return ParachuteState.IsActive && ParachuteState.Player != null && controller.Entity == ParachuteState.Player;
    }
}
