using UnityEngine;

/// <summary>
/// The canopy object and its animation. It is parented to the player entity
/// root, so it follows the physics position in both first and third person. All
/// motion is code driven; the prefab must contain no colliders or rigidbodies.
/// In third person the riser lines are re-aimed at the character's shoulder
/// bones every frame, so they stay attached whatever pose the body is in.
/// </summary>
internal static class ParachuteVisual
{
    private static GameObject canopyInstance;
    private static Transform canopy;
    private static float openStart;
    private static float stowStart;
    private static float canopyScale = 1f;
    private static float canopyHeight = 3.2f;
    private static float currentRoll;
    private static float currentPitch;
    private static EntityPlayerLocal owner;
    private static LineRenderer[] risers;
    private static Vector3[] riserRestEnds;

    internal static void Deploy(EntityPlayerLocal player, ParachuteConfig config)
    {
        Destroy();
        canopyScale = config.CanopyScale;
        canopyHeight = config.CanopyHeight;
        openStart = Time.time;

        GameObject prefab = ParachuteAssets.CanopyPrefab;
        if (prefab == null || player == null) return;

        canopyInstance = Object.Instantiate(prefab, player.transform, false);
        canopyInstance.name = "ParachuteCanopyInstance";
        canopy = canopyInstance.transform;
        canopy.localPosition = new Vector3(0f, canopyHeight, -0.2f);
        canopy.localRotation = Quaternion.Euler(-30f, 0f, 0f);
        canopy.localScale = new Vector3(0.15f, 0.4f, 0.15f) * canopyScale;

        owner = player;
        risers = canopyInstance.GetComponentsInChildren<LineRenderer>();
        riserRestEnds = new Vector3[risers.Length];
        for (int i = 0; i < risers.Length; i++) riserRestEnds[i] = risers[i].GetPosition(1);
    }

    internal static void Stow(ParachuteConfig config)
    {
        stowStart = Time.time;
    }

    internal static void Tick(ParachuteConfig config, ChutePhase phase, float turn, bool forward, bool back)
    {
        if (canopy == null)
        {
            if (phase == ChutePhase.Stowed) return;
            return;
        }

        if (phase == ChutePhase.Deploying)
        {
            float t = config.DeployTime > 0f ? Mathf.Clamp01((Time.time - openStart) / config.DeployTime) : 1f;
            float eased = ParachuteFlight.BackOut(t);
            Vector3 from = new Vector3(0.15f, 0.4f, 0.15f) * canopyScale;
            canopy.localScale = Vector3.LerpUnclamped(from, Vector3.one * canopyScale, eased);
            canopy.localRotation = Quaternion.Euler(Mathf.Lerp(-30f, 0f, t), 0f, 0f);
        }
        else if (phase == ChutePhase.Open)
        {
            float sway = Mathf.Sin((Time.time - openStart) * 2f * Mathf.PI * 0.6f) * 2f;
            float targetRoll = -turn * 12f;
            float targetPitch = forward ? 6f : (back ? -8f : 0f);
            currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * 3f);
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * 3f);
            canopy.localScale = Vector3.one * canopyScale;
            canopy.localPosition = new Vector3(0f, canopyHeight, -0.2f);
            canopy.localRotation = Quaternion.Euler(currentPitch, 0f, currentRoll + sway);
        }
        else if (phase == ChutePhase.Stowing)
        {
            float t = config.StowAnimTime > 0f ? Mathf.Clamp01((Time.time - stowStart) / config.StowAnimTime) : 1f;
            Vector3 full = Vector3.one * canopyScale;
            Vector3 collapsed = new Vector3(0.3f, 0.1f, 0.3f) * canopyScale;
            canopy.localScale = Vector3.Lerp(full, collapsed, t);
            if (t >= 1f)
            {
                Destroy();
                return;
            }
        }

        UpdateRisers();
    }

    /// <summary>
    /// Ends each riser at the shoulder on its side. The prefab's fixed end point
    /// is kept in first person, where the body is not drawn, and whenever the
    /// bones cannot be found.
    /// </summary>
    private static void UpdateRisers()
    {
        if (risers == null || owner == null) return;

        Transform left = null;
        Transform right = null;
        if (!owner.bFirstPersonView && owner.emodel is EModelSDCS model && model.boneCatalog != null)
        {
            model.boneCatalog.TryGetValue("LeftArm", out left);
            model.boneCatalog.TryGetValue("RightArm", out right);
        }

        Transform body = owner.transform;
        for (int i = 0; i < risers.Length; i++)
        {
            LineRenderer riser = risers[i];
            if (riser == null) continue;

            // The canopy end of the line says which riser this is: -x is the
            // left side, -z is the rear rows.
            Vector3 anchor = riser.GetPosition(0);
            Transform shoulder = anchor.x < 0f ? left : right;
            Vector3 end = riserRestEnds[i];
            if (shoulder != null)
            {
                Vector3 world = shoulder.position + body.up * 0.05f;
                if (anchor.z < 0f) world -= body.forward * 0.08f;
                end = riser.transform.InverseTransformPoint(world);
            }
            riser.SetPosition(1, end);
        }
    }

    internal static void Destroy()
    {
        if (canopyInstance != null) Object.Destroy(canopyInstance);
        canopyInstance = null;
        canopy = null;
        owner = null;
        risers = null;
        riserRestEnds = null;
        currentRoll = 0f;
        currentPitch = 0f;
    }
}
