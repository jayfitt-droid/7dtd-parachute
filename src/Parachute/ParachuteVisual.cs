using UnityEngine;

/// <summary>
/// The local player's canopy. A thin wrapper over one CanopyRig; other players'
/// canopies are separate rigs owned by ParachuteRemote.
/// </summary>
internal static class ParachuteVisual
{
    private static CanopyRig local;

    internal static void Deploy(EntityPlayerLocal player, ParachuteConfig config)
    {
        Destroy();
        local = CanopyRig.Create(player, config);
    }

    internal static void Stow(ParachuteConfig config)
    {
        if (local != null) local.Stow();
    }

    internal static void Tick(ParachuteConfig config, float turn, bool forward, bool back)
    {
        if (local != null && !local.Tick(config, turn, forward, back)) local = null;
    }

    internal static void Destroy()
    {
        if (local != null) local.Destroy();
        local = null;
    }
}

/// <summary>
/// One canopy object and its animation, for any player. It is parented to the
/// player entity root, so it follows the position in both first and third person
/// (for another player, the position the game interpolates from the network).
/// All motion is code driven; the prefab must contain no colliders or
/// rigidbodies. A riser strap rises from each shoulder and the suspension lines
/// on that side gather at its top; both are re-aimed every frame, from the worn
/// pack's shoulder straps (or the shoulder bones) in third person, so they stay
/// attached in any pose.
///
/// The rig keeps its own clock: it opens over deploy_time from Create and, once
/// Stow is called, collapses over stow_anim_time and destroys itself, whatever
/// the owner's state does in the meantime.
/// </summary>
internal sealed class CanopyRig
{
    /// <summary>Length of the strap from the shoulder to where the lines gather.</summary>
    private const float StrapLength = 0.45f;

    private GameObject instance;
    private Transform canopy;
    private EntityAlive owner;
    private float openStart;
    private float stowStart;
    private bool stowing;
    private float canopyScale = 1f;
    private float canopyHeight = 3.2f;
    private float currentRoll;
    private float currentPitch;
    private LineRenderer[] risers;
    private readonly LineRenderer[] straps = new LineRenderer[2];
    private readonly Transform[] links = new Transform[2];
    private readonly Transform[] strapTops = new Transform[2];

    internal bool Stowing => stowing;

    /// <summary>Null if the model is missing (no bundle) or there is no owner.</summary>
    internal static CanopyRig Create(EntityAlive owner, ParachuteConfig config)
    {
        if (owner == null || GameManager.IsDedicatedServer) return null;
        GameObject prefab = ParachuteAssets.CanopyPrefab;
        if (prefab == null) return null;

        var rig = new CanopyRig
        {
            owner = owner,
            canopyScale = config.CanopyScale,
            canopyHeight = config.CanopyHeight,
            openStart = Time.time,
        };
        rig.instance = Object.Instantiate(prefab, owner.transform, false);
        rig.instance.name = "ParachuteCanopyInstance";
        rig.canopy = rig.instance.transform;
        rig.canopy.localPosition = new Vector3(0f, rig.canopyHeight, -0.2f);
        rig.canopy.localRotation = Quaternion.Euler(-30f, 0f, 0f);
        rig.canopy.localScale = new Vector3(0.15f, 0.4f, 0.15f) * rig.canopyScale;

        var lines = new System.Collections.Generic.List<LineRenderer>();
        foreach (LineRenderer line in rig.instance.GetComponentsInChildren<LineRenderer>())
        {
            if (line.name == "Strap_L") rig.straps[0] = line;
            else if (line.name == "Strap_R") rig.straps[1] = line;
            else lines.Add(line);
        }
        rig.risers = lines.ToArray();
        rig.links[0] = rig.canopy.Find("Link_L");
        rig.links[1] = rig.canopy.Find("Link_R");
        return rig;
    }

    internal void Stow()
    {
        if (stowing) return;
        stowing = true;
        stowStart = Time.time;
    }

    /// <summary>Animates one frame. False once the rig is gone (collapsed, or its owner was destroyed).</summary>
    internal bool Tick(ParachuteConfig config, float turn, bool forward, bool back)
    {
        if (canopy == null)
        {
            Destroy();
            return false;
        }

        if (stowing)
        {
            float t = config.StowAnimTime > 0f ? Mathf.Clamp01((Time.time - stowStart) / config.StowAnimTime) : 1f;
            Vector3 full = Vector3.one * canopyScale;
            Vector3 collapsed = new Vector3(0.3f, 0.1f, 0.3f) * canopyScale;
            canopy.localScale = Vector3.Lerp(full, collapsed, t);
            if (t >= 1f)
            {
                Destroy();
                return false;
            }
        }
        else
        {
            float t = config.DeployTime > 0f ? Mathf.Clamp01((Time.time - openStart) / config.DeployTime) : 1f;
            if (t < 1f)
            {
                float eased = ParachuteFlight.BackOut(t);
                Vector3 from = new Vector3(0.15f, 0.4f, 0.15f) * canopyScale;
                canopy.localScale = Vector3.LerpUnclamped(from, Vector3.one * canopyScale, eased);
                canopy.localRotation = Quaternion.Euler(Mathf.Lerp(-30f, 0f, t), 0f, 0f);
            }
            else
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
        }

        UpdateRisers();
        return true;
    }

    /// <summary>
    /// Places the two riser straps and gathers the lines at their tops. Side 0
    /// is the player's left. Each strap starts at the shoulder and points at the
    /// middle of that side's line attachments on the canopy. In third person the
    /// shoulder is the top of the worn pack's shoulder strap, or the upper-arm
    /// bone without a pack; in first person, where the body is not drawn, or if
    /// neither can be found, it is a fixed point beside the head. Other players
    /// are always seen in third person.
    /// </summary>
    private void UpdateRisers()
    {
        if (risers == null || owner == null) return;

        var localOwner = owner as EntityPlayerLocal;
        bool firstPerson = localOwner != null && localOwner.bFirstPersonView;
        Transform leftBone = null;
        Transform rightBone = null;
        if (!firstPerson)
        {
            if (localOwner != null)
            {
                leftBone = ParachuteWorn.StrapTop(0);
                rightBone = ParachuteWorn.StrapTop(1);
            }
            else
            {
                // Found once and kept; a destroyed pack reads as null and is looked up again.
                for (int side = 0; side < 2; side++)
                    if (strapTops[side] == null) strapTops[side] = ParachuteWorn.FindStrapTop(owner, side);
                leftBone = strapTops[0] != null && strapTops[0].gameObject.activeInHierarchy ? strapTops[0] : null;
                rightBone = strapTops[1] != null && strapTops[1].gameObject.activeInHierarchy ? strapTops[1] : null;
            }
        }
        bool onPack = leftBone != null && rightBone != null;
        if (!onPack && !firstPerson && owner.emodel is EModelSDCS model && model.boneCatalog != null)
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
            Vector3 shoulder = bone == null
                ? body.TransformPoint(new Vector3(side == 0 ? -0.22f : 0.22f, 1.42f, -0.05f))
                : onPack ? bone.position : bone.position + body.up * 0.05f;
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

    internal void Destroy()
    {
        if (instance != null) Object.Destroy(instance);
        instance = null;
        canopy = null;
        owner = null;
        risers = null;
        straps[0] = straps[1] = null;
        links[0] = links[1] = null;
        strapTops[0] = strapTops[1] = null;
    }
}
