using UnityEngine;

internal enum ChutePhase
{
    Stowed,
    Deploying,
    Open,
    Stowing
}

/// <summary>
/// The single source of truth for the local player's parachute. Phase 1 is
/// single-player/host only, so one static state is enough; Phase 2 gives each
/// remote player their own visual driven from replicated state.
/// </summary>
internal static class ParachuteState
{
    internal static ChutePhase Phase = ChutePhase.Stowed;
    internal static EntityPlayerLocal Player;
    internal static float PhaseStartTime;
    internal static float InitialFallSpeed;
    internal static float LandingGraceUntil;
    internal static float RedeployReadyTime;
    internal static float CurrentGlideSpeed;
    internal static float CurrentFallSpeed;
    /// <summary>Sideways speed in m/s; positive is to the player's right.</summary>
    internal static float CurrentStrafeSpeed;
    internal static string LastStowReason = "";
    internal static bool LastForward;
    internal static bool LastBack;
    internal static float LastTurn;

    internal static bool IsActive => Phase == ChutePhase.Deploying || Phase == ChutePhase.Open;
    internal static bool InGrace => Time.time < LandingGraceUntil;

    internal static void Reset()
    {
        Phase = ChutePhase.Stowed;
        PhaseStartTime = 0f;
        InitialFallSpeed = 0f;
        LandingGraceUntil = 0f;
        RedeployReadyTime = 0f;
        CurrentGlideSpeed = 0f;
        CurrentFallSpeed = 0f;
        CurrentStrafeSpeed = 0f;
        LastStowReason = "";
        LastForward = false;
        LastBack = false;
        LastTurn = 0f;
    }
}
