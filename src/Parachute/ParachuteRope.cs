using System;

/// <summary>
/// Whether Jason's Grappling Hook mod has a rope attached, so the Jump press that
/// lets go of the rope does not also open the canopy. Neither mod references the
/// other: the Grappling Hook publishes a named value on the AppDomain
/// (Grapple.AttachedSignal there) and this reads it. Without that mod the value is
/// never set and this is always false.
/// </summary>
internal static class ParachuteRope
{
    /// <summary>Must match Grapple.AttachedSignal in the Grappling Hook source.</summary>
    private const string AttachedSignal = "GrapplingHook.Attached";

    internal static bool Attached
    {
        get { return AppDomain.CurrentDomain.GetData(AttachedSignal) is bool attached && attached; }
    }
}
