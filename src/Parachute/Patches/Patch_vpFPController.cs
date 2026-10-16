using HarmonyLib;
using UnityEngine;

/// <summary>
/// The flight model. Both postfixes only touch the local player's controller
/// while the canopy is deploying or open.
/// </summary>
[HarmonyPatch(typeof(vp_FPController))]
internal static class Patch_vpFPController
{
    [HarmonyPostfix]
    [HarmonyPatch("UpdateThrottleWalk")]
    private static void ThrottlePostfix(vp_FPController __instance)
    {
        if (!IsLocalActive(__instance)) return;

        // First person: the body faces the view. Third person: the body turns
        // toward the view over a moment (Patch_Camera), so fly where the camera
        // looks instead of where the body happens to face.
        Vector3 direction;
        EntityPlayerLocal local = __instance.localPlayer;
        if (local.bFirstPersonView)
        {
            direction = __instance.Transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return;
            direction.Normalize();
        }
        else
        {
            float yaw = ParachuteFlight.LookYaw(local, __instance.Transform.eulerAngles.y) * Mathf.Deg2Rad;
            direction = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        }

        // FixedMove multiplies x/z by m_SpeedModifier, so divide it back out.
        float speedModifier = Mathf.Max(0.01f, __instance.m_SpeedModifier);
        Vector3 right = new Vector3(direction.z, 0f, -direction.x);
        Vector3 flat = (direction * ParachuteState.CurrentGlideSpeed + right * ParachuteState.CurrentStrafeSpeed)
            / ParachuteFlight.VpStepsPerSecondFactor;
        __instance.m_MotorThrottle.x = flat.x / speedModifier;
        __instance.m_MotorThrottle.z = flat.z / speedModifier;
    }

    [HarmonyPostfix]
    [HarmonyPatch("UpdateForces")]
    private static void ForcesPostfix(vp_FPController __instance)
    {
        if (!IsLocalActive(__instance)) return;

        // Brake only: never push the player down faster than they already fall.
        float target = -ParachuteState.CurrentFallSpeed / ParachuteFlight.VpStepsPerSecondFactor;
        if (__instance.m_FallSpeed < target) __instance.m_FallSpeed = target;

        // Damp external vertical shoves, then erase the fall-height memory so the
        // height-based impact calculation sees a fall from zero.
        if (__instance.m_ExternalForce.y > 0f) __instance.m_ExternalForce.y = 0f;
        __instance.m_ExternalForce.y *= 0.5f;
        float worldY = __instance.Transform.position.y + Origin.position.y;
        __instance.m_MaxHeight = worldY;
        __instance.m_MaxHeightInitialFallSpeed = 0f;
    }

    private static bool IsLocalActive(vp_FPController controller)
    {
        if (!ParachuteState.IsActive) return false;
        EntityPlayerLocal local = controller.localPlayer;
        return local != null && local == ParachuteState.Player;
    }
}
