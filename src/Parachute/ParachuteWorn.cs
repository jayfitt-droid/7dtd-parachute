using UnityEngine;

internal enum WornKind
{
    World,
    Ui
}

/// <summary>Holds the rotation that lines the pack's axes up with the body on the bone it hangs from.</summary>
internal sealed class ParachutePackPlacement : MonoBehaviour
{
    internal Quaternion BaseRotation = Quaternion.identity;
}

/// <summary>
/// The pack worn on the back while the parachute is installed in the chest
/// armor: on the third-person rig of every player (the local one and, for
/// clients with this DLL, other players) and on the inventory preview rig.
/// Instances are parented under Spine3, which survives the game's equipment
/// rebuilds, and are found by name before being created, so a rebuild never
/// doubles or strands them. The body is not drawn in first person, so neither
/// is the pack. Ported from the GliderSuit mod's GliderWorn.
/// </summary>
internal static class ParachuteWorn
{
    internal const string RootName = "ParachutePackWorn";

    private static GameObject localWorld;
    private static GameObject localPreview;
    private static readonly Transform[] strapTops = new Transform[2];

    /// <summary>Called from the SDCS postfixes and as the controller's backstop.</summary>
    internal static void Sync(EntityAlive entity, SDCSUtils.TransformCatalog catalog, WornKind kind)
    {
        if (entity == null || catalog == null || GameManager.IsDedicatedServer) return;

        Transform spine = Bone(catalog, "Spine3");
        if (spine == null) return;

        ParachuteConfig config = ParachuteMod.Settings;
        bool hasParachute = ParachuteEquip.HasParachute(entity);
        bool firstPerson = IsFirstPerson(entity);
        bool wanted = config.ShowPack && hasParachute && (kind == WornKind.Ui || !firstPerson);
        bool local = entity is EntityPlayerLocal;
        Transform existing = spine.Find(RootName);

        if (!wanted)
        {
            if (existing != null)
            {
                Object.Destroy(existing.gameObject);
                if (config.Debug)
                    Log.Out("[Parachute] Worn " + kind + " pack removed from " + entity.EntityName +
                        ": installed=" + hasParachute + " firstPerson=" + firstPerson);
            }
            if (local) Track(kind, null);
            return;
        }

        ParachutePackPlacement pack = existing != null ? existing.GetComponent<ParachutePackPlacement>() : null;
        if (pack == null)
        {
            if (existing != null) Object.Destroy(existing.gameObject);
            GameObject prefab = ParachuteAssets.PackPrefab;
            if (prefab == null) return;
            GameObject instance = Object.Instantiate(prefab, spine, false);
            instance.name = RootName;
            pack = instance.AddComponent<ParachutePackPlacement>();
            pack.BaseRotation = BodyAlignment(catalog, spine);
            if (config.Debug)
                Log.Out("[Parachute] Worn " + kind + " pack attached to " + entity.EntityName + "'s " + spine.name +
                    ", body alignment " + pack.BaseRotation.eulerAngles);
        }
        Place(pack, config);
        if (local) Track(kind, pack.gameObject);
    }

    private static void Track(WornKind kind, GameObject instance)
    {
        if (kind == WornKind.Ui)
        {
            localPreview = instance;
            return;
        }
        localWorld = instance;
        strapTops[0] = instance != null ? instance.transform.Find("StrapTop_L") : null;
        strapTops[1] = instance != null ? instance.transform.Find("StrapTop_R") : null;
    }

    /// <summary>True while the local player's third-person pack is attached and alive.</summary>
    internal static bool WorldPresent => localWorld != null;

    /// <summary>
    /// The top of the local player's shoulder strap on that side (0 is the left),
    /// where the riser strap starts; null in first person or without a pack.
    /// </summary>
    internal static Transform StrapTop(int side)
    {
        if (localWorld == null || !localWorld.activeInHierarchy) return null;
        return strapTops[side];
    }

    /// <summary>
    /// The top of another player's shoulder strap on that side (0 is the left),
    /// found on their rig; null without a pack.
    /// </summary>
    internal static Transform FindStrapTop(EntityAlive entity, int side)
    {
        EModelSDCS model = entity != null ? entity.emodel as EModelSDCS : null;
        Transform spine = model != null ? Bone(model.boneCatalog, "Spine3") : null;
        Transform pack = spine != null ? spine.Find(RootName) : null;
        return pack != null ? pack.Find(side == 0 ? "StrapTop_L" : "StrapTop_R") : null;
    }

    /// <summary>
    /// Backstop for another player, called a few times a second. The game does not
    /// rebuild a remote player's rig when only the mods in their worn armor change,
    /// so installing or removing the parachute would otherwise not show until their
    /// next equipment change. Sync is idempotent, so this also re-applies the pack
    /// placement after "chute reload".
    /// </summary>
    internal static void SyncRemote(EntityPlayer player)
    {
        if (player == null || player is EntityPlayerLocal) return;
        EModelSDCS model = player.emodel as EModelSDCS;
        if (model == null || model.boneCatalog == null) return;
        Sync(player, model.boneCatalog, WornKind.World);
    }

    /// <summary>Re-applies placement live (used by "chute reload").</summary>
    internal static void ReapplyPlacement()
    {
        ParachuteConfig config = ParachuteMod.Settings;
        foreach (GameObject instance in new[] { localWorld, localPreview })
        {
            ParachutePackPlacement pack = instance != null ? instance.GetComponent<ParachutePackPlacement>() : null;
            if (pack != null) Place(pack, config);
        }
    }

    /// <summary>offset and rotation are in the body's axes: right, up the spine, out of the chest.</summary>
    private static void Place(ParachutePackPlacement pack, ParachuteConfig config)
    {
        Transform transform = pack.transform;
        transform.localPosition = pack.BaseRotation * config.PackOffset;
        transform.localRotation = pack.BaseRotation * Quaternion.Euler(config.PackRotation);
        transform.localScale = Vector3.one * config.PackScale;

        // The chest pieces hang from "Front", hinged at the strap tops' depth, so
        // stretching it along z moves them out to the chest without moving the tops.
        Transform front = transform.Find("Front");
        if (front != null) front.localScale = new Vector3(1f, 1f, config.PackChestDepth);
    }

    /// <summary>
    /// The rotation, in the bone's own space, that points the pack's right, up and
    /// forward along the body's. The bone's axes cannot be assumed, so the body's
    /// are read from where the neck and the two shoulders sit relative to the
    /// bone. Those offsets are bone lengths: they do not change with the animation.
    /// </summary>
    private static Quaternion BodyAlignment(SDCSUtils.TransformCatalog catalog, Transform spine)
    {
        Transform neck = Bone(catalog, "Neck");
        Transform left = Bone(catalog, "LeftShoulder");
        Transform right = Bone(catalog, "RightShoulder");
        if (neck == null || left == null || right == null)
        {
            Log.Warning("[Parachute] Neck or shoulder bones missing; the worn pack may sit crooked.");
            return Quaternion.identity;
        }
        Vector3 up = spine.InverseTransformPoint(neck.position);
        Vector3 across = spine.InverseTransformPoint(right.position) - spine.InverseTransformPoint(left.position);
        across = Vector3.ProjectOnPlane(across, up);
        if (up.sqrMagnitude < 1e-8f || across.sqrMagnitude < 1e-8f) return Quaternion.identity;
        Vector3 forward = Vector3.Cross(across.normalized, up.normalized);
        return Quaternion.LookRotation(forward, up);
    }

    /// <summary>Per frame for the local player: the pack is hidden in first person.</summary>
    internal static void Tick(EntityPlayerLocal player)
    {
        if (player == null || localWorld == null) return;
        bool visible = !player.bFirstPersonView;
        if (localWorld.activeSelf != visible) localWorld.SetActive(visible);
    }

    internal static void Destroy()
    {
        if (localWorld != null) Object.Destroy(localWorld);
        if (localPreview != null) Object.Destroy(localPreview);
        Track(WornKind.World, null);
        Track(WornKind.Ui, null);
    }

    private static bool IsFirstPerson(EntityAlive entity)
    {
        var local = entity as EntityPlayerLocal;
        return local != null && local.bFirstPersonView;
    }

    private static Transform Bone(SDCSUtils.TransformCatalog catalog, string name)
    {
        if (catalog == null || !catalog.ContainsKey(name)) return null;
        return catalog[name];
    }
}
