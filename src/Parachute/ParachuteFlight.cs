using UnityEngine;

/// <summary>
/// Pure flight math, kept apart from the game types so it is easy to reason
/// about and test. All speeds are in meters per second and all times in
/// seconds.
/// </summary>
internal static class ParachuteFlight
{
    /// <summary>
    /// vp_FPController.Delta is Time.deltaTime * 60, so a per-step velocity
    /// value of v moves the player v * 60 meters per second. Never change this
    /// without re-checking FixedMove in the decompiled source.
    /// </summary>
    internal const float VpStepsPerSecondFactor = 60f;

    /// <summary>Reads the movement actions; analog sticks and the D-pad both work.</summary>
    internal static void ReadInput(PlayerActionsLocal input, out bool forward, out bool back, out float turn)
    {
        forward = false;
        back = false;
        turn = 0f;
        if (input == null) return;

        float y = input.Move != null ? input.Move.Value.y : 0f;
        float x = input.Move != null ? input.Move.Value.x : 0f;
        if (input.MoveForward != null && input.MoveForward.IsPressed) y = Mathf.Max(y, 1f);
        if (input.MoveBack != null && input.MoveBack.IsPressed) y = Mathf.Min(y, -1f);
        if (input.MoveRight != null && input.MoveRight.IsPressed) x = Mathf.Max(x, 1f);
        if (input.MoveLeft != null && input.MoveLeft.IsPressed) x = Mathf.Min(x, -1f);

        if (y > 0.3f) forward = true;
        else if (y < -0.3f) back = true;
        if (Mathf.Abs(x) < 0.3f) x = 0f;
        turn = Mathf.Clamp(x, -1f, 1f);
    }

    internal static void Targets(ParachuteConfig config, bool forward, bool back, out float glide, out float fall)
    {
        if (forward)
        {
            glide = config.GlideSpeedForward;
            fall = config.FallSpeedForward;
        }
        else if (back)
        {
            glide = config.GlideSpeedBack;
            fall = config.FallSpeedBack;
        }
        else
        {
            glide = config.GlideSpeed;
            fall = config.FallSpeed;
        }
    }

    /// <summary>Sideways speed asked for by Left/Right; zero when they turn instead.</summary>
    internal static float StrafeTarget(ParachuteConfig config, float turn)
    {
        return config.LeftRight == LeftRightMode.Strafe ? turn * config.StrafeSpeed : 0f;
    }

    internal static float Approach(float current, float target, float response, float deltaTime)
    {
        return Mathf.MoveTowards(current, target, Mathf.Max(0f, response) * deltaTime);
    }

    /// <summary>Ease-out for the opening brake, so the canopy does not stop instantly.</summary>
    internal static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    internal static float BackOut(float t)
    {
        const float overshoot = 1.7f;
        t -= 1f;
        return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
    }

    /// <summary>
    /// The compass direction the player is looking, in degrees. First person keeps
    /// it in the camera's Yaw. Third person does not: there the camera orbits the
    /// character on yaw3P, which is the direction from the character back to the
    /// camera, half a turn round from the view (vp_FPCamera sets Yaw = yaw3P - 180
    /// when it switches back). Ported from the GliderSuit mod.
    /// </summary>
    internal static float LookYaw(EntityPlayerLocal player, float fallback)
    {
        vp_FPCamera camera = player != null ? player.vp_FPCamera : null;
        if (camera == null) return fallback;
        return player.bFirstPersonView ? camera.Yaw : camera.yaw3P - 180f;
    }

    /// <summary>Turns the view by this many degrees, in whichever of the two the current view uses.</summary>
    internal static void TurnLook(EntityPlayerLocal player, float degrees)
    {
        vp_FPCamera camera = player != null ? player.vp_FPCamera : null;
        if (camera == null) return;
        if (player.bFirstPersonView) camera.Yaw += degrees;
        else camera.yaw3P += degrees;
    }
}
