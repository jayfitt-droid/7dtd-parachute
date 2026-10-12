using UnityEngine;

/// <summary>
/// The canopy object and its animation. It is parented to the player entity
/// root, so it follows the physics position in both first and third person. All
/// motion is code driven; the prefab must contain no colliders or rigidbodies.
/// A riser strap rises from each shoulder and the suspension lines on that side
/// gather at its top; both are re-aimed every frame, at the character's
/// shoulder bones in third person, so they stay attached in any pose.
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
    private static readonly LineRenderer[] straps = new LineRenderer[2];
    private static readonly Transform[] links = new Transform[2];

    /// <summary>Length of the strap from the shoulder to where the lines gather.</summary>
    private const float StrapLength = 0.45f;

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
        var lines = new System.Collections.Generic.List<LineRenderer>();
        foreach (LineRenderer line in canopyInstance.GetComponentsInChildren<LineRenderer>())
        {
            if (line.name == "Strap_L") straps[0] = line;
            else if (line.name == "Strap_R") straps[1] = line;
            else lines.Add(line);
        }
        risers = lines.ToArray();
        links[0] = canopy.Find("Link_L");
        links[1] = canopy.Find("Link_R");
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
    /// Places the two riser straps and gathers the lines at their tops. Side 0
    /// is the player's left. Each strap starts at the shoulder (the upper-arm
    /// bone in third person; a fixed point beside the head in first person,
    /// where the body is not drawn, or if the bone cannot be found) and points
    /// at the middle of that side's line attachments on the canopy.
    /// </summary>
    private static void UpdateRisers()
    {
        if (risers == null || owner == null) return;

        Transform leftBone = null;
        Transform rightBone = null;
        if (!owner.bFirstPersonView && owner.emodel is EModelSDCS model && model.boneCatalog != null)
        {
            model.boneCatalog.TryGetValue("LeftArm", out leftBone);
            model.boneCatalog.TryGetValue("RightArm", out rightBone);
        }

        // The canopy end of a line says which side it is on: -x is the left.
        var centroid = new Vector3[2];
        var count = new int[2];
        for (int i = 0; i < risers.Length; i++)
        {
            LineRenderer riser = risers[i];
            if (riser == null) continue;
            Vector3 anchor = riser.GetPosition(0);
            int side = anchor.x < 0f ? 0 : 1;
            centroid[side] += riser.transform.TransformPoint(anchor);
            count[side]++;
        }

        Transform body = owner.transform;
        var top = new Vector3[2];
        for (int side = 0; side < 2; side++)
        {
            Transform bone = side == 0 ? leftBone : rightBone;
            Vector3 shoulder = bone != null
                ? bone.position + body.up * 0.05f
                : body.TransformPoint(new Vector3(side == 0 ? -0.22f : 0.22f, 1.42f, -0.05f));
            Vector3 toCanopy = count[side] > 0 ? centroid[side] / count[side] - shoulder : body.up;
            if (toCanopy.sqrMagnitude < 0.0001f) toCanopy = body.up;
            // Halfway between straight up and straight at the canopy, so the
            // straps stand up off the shoulders instead of splaying out.
            Vector3 direction = (toCanopy.normalized + body.up).normalized;
            top[side] = shoulder + direction * StrapLength;

            LineRenderer strap = straps[side];
            if (strap != null)
            {
                strap.SetPosition(0, strap.transform.InverseTransformPoint(shoulder));
                strap.SetPosition(1, strap.transform.InverseTransformPoint(top[side]));
            }
            if (links[side] != null) links[side].position = top[side];
        }

        for (int i = 0; i < risers.Length; i++)
        {
            LineRenderer riser = risers[i];
            if (riser == null) continue;
            int side = riser.GetPosition(0).x < 0f ? 0 : 1;
            riser.SetPosition(1, riser.transform.InverseTransformPoint(top[side]));
        }
    }

    internal static void Destroy()
    {
        if (canopyInstance != null) Object.Destroy(canopyInstance);
        canopyInstance = null;
        canopy = null;
        owner = null;
        risers = null;
        straps[0] = straps[1] = null;
        links[0] = links[1] = null;
        currentRoll = 0f;
        currentPitch = 0f;
    }
}
