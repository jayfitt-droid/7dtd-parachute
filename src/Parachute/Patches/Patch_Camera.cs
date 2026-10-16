using HarmonyLib;

/// <summary>
/// In third person, keeps the character facing the way the camera looks while the
/// canopy is open. The game turns a third-person character to face whichever way
/// the movement keys point, so Left/Right (strafe) and Back spun the character
/// round to face sideways or back at the camera. Ported from the GliderSuit mod.
/// </summary>
[HarmonyPatch(typeof(vp_FPCamera))]
internal static class Patch_Camera
{
    [HarmonyPostfix]
    [HarmonyPatch("LateUpdate")]
    private static void Postfix(vp_FPCamera __instance)
    {
        try
        {
            if (!ParachuteState.IsActive) return;
            EntityPlayerLocal local = __instance.FPController != null ? __instance.FPController.localPlayer : null;
            if (local == null || local != ParachuteState.Player || local.bFirstPersonView) return;

            // vp_FPCamera.Update turns the character toward targetRotation3P; this
            // runs after Update3rdPerson has set it from the movement keys.
            __instance.targetRotation3P = ParachuteFlight.LookYaw(local, __instance.targetRotation3P);
        }
        catch (System.Exception)
        {
            // A camera effect is never worth breaking a frame over.
        }
    }
}
