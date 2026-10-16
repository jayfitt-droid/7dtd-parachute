using HarmonyLib;
using UnityEngine;

/// <summary>
/// While the canopy is open in third person, the camera sits where the game puts
/// it for a vehicle instead of over the shoulder: centred behind the player,
/// higher, and further back the faster the parachute glides. Ported from the
/// GliderSuit mod (its D40).
///
/// The vehicle camera is the same vp_FPCamera.Update3rdPerson code; the only
/// difference is the offset it is fed. On foot EntityPlayerLocal.FrameUpdateCamera
/// sets Position3rdPersonOffset to (0.4, 1.65, 1.2) every frame; in a vehicle
/// EntityVehicle.GetCameraOffset feeds (0, 0, distance) around a point 1.8 m up.
/// This postfix overwrites the on-foot value the same way. Nothing else changes,
/// so the toolbar and weapons work as before. While aiming, the game's own over-
/// the-shoulder offset is left alone so the sights still come in close.
/// </summary>
[HarmonyPatch(typeof(EntityPlayerLocal))]
internal static class Patch_ChaseCamera
{
    [HarmonyPostfix]
    [HarmonyPatch("FrameUpdateCamera")]
    private static void Postfix(EntityPlayerLocal __instance)
    {
        try
        {
            ParachuteConfig config = ParachuteMod.Settings;
            if (!config.ChaseCamera || !ParachuteState.IsActive) return;
            if (__instance != ParachuteState.Player || __instance.bFirstPersonView) return;
            if (__instance.AttachedToEntity != null || __instance.AimingGun) return;
            vp_FPCamera camera = __instance.vp_FPCamera;
            if (camera == null || camera.Locked3rdPerson) return;

            // vp_FPCamera.Update eases its current offset toward this at 10/s, so
            // the change on opening and stowing is already smooth.
            float horizontal = new Vector2(ParachuteState.CurrentGlideSpeed, ParachuteState.CurrentStrafeSpeed).magnitude;
            float fraction = config.GlideSpeedForward > 0f ? Mathf.Clamp01(horizontal / config.GlideSpeedForward) : 0f;
            float distance = Mathf.Lerp(config.ChaseCameraDistanceMin, config.ChaseCameraDistanceMax, fraction);
            camera.Position3rdPersonOffset = new Vector3(0f, config.ChaseCameraHeight, distance);
        }
        catch (System.Exception)
        {
            // A camera effect is never worth breaking a frame over.
        }
    }
}
