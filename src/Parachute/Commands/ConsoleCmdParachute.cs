using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ConsoleCmdParachute : ConsoleCmdAbstract
{
    private const string ModItem = "modArmorParachute";
    private const string SchematicItem = "modArmorParachuteSchematic";
    private const string DefaultOutfit = "armorScavengerOutfit";

    public override string[] getCommands() { return new[] { "chute", "parachute" }; }
    public override string getDescription() { return "Parachute test commands"; }
    public override string getHelp()
    {
        return "chute give | outfit [item] [quality] | mod [count] | schematic [count] | kit | " +
               "up [m] | deploy | stow | status | reload | debug [on|off]";
    }
    public override bool IsExecuteOnClient => true;

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        string command = args.Count == 0 ? "help" : args[0].ToLowerInvariant();
        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        if (player == null)
        {
            Log.Out("[Parachute] No local player; run this from a game client.");
            return;
        }
        try
        {
            switch (command)
            {
                case "give": GiveAll(player); break;
                case "outfit": GiveOutfit(args, player); break;
                case "mod": GiveItem(player, ModItem, Count(args, 1, 1)); break;
                case "schematic": GiveItem(player, SchematicItem, Count(args, 1, 1)); break;
                case "kit": GiveKit(player); break;
                case "up": Up(args, player); break;
                case "deploy": ParachuteController.ForceDeploy(player); break;
                case "stow": ParachuteController.ForceStow(); Log.Out("[Parachute] Stow requested."); break;
                case "status": Status(player); break;
                case "reload": ParachuteMod.Reload(); Status(player); break;
                case "debug": Debug(args); break;
                default: Log.Out("[Parachute] " + getHelp()); break;
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[Parachute] Command failed: " + ex);
        }
    }

    private static void GiveAll(EntityPlayerLocal player)
    {
        GiveOutfit(new List<string> { "outfit" }, player);
        GiveItem(player, SchematicItem, 1);
        GiveItem(player, ModItem, 1);
    }

    private static void GiveOutfit(List<string> args, EntityPlayerLocal player)
    {
        string name = args.Count > 1 ? args[1] : DefaultOutfit;
        int quality = 6;
        if (args.Count > 2 && (!int.TryParse(args[2], out quality) || quality < 1 || quality > 6))
        {
            Log.Out("[Parachute] Quality must be 1-6.");
            return;
        }
        ItemValue prototype = ItemClass.GetItem(name, false);
        if (prototype == null || prototype.IsEmpty())
        {
            Log.Out("[Parachute] Unknown item: " + name);
            return;
        }
        if (prototype.ItemClass == null || !prototype.ItemClass.ItemTags.Test_AnySet(ArmorChestTag))
        {
            Log.Out("[Parachute] " + name + " is not chest armor (armorChest).");
            return;
        }
        ItemValue outfit = new ItemValue(prototype.type, quality, quality, false);
        GiveStack(player, new ItemStack(outfit, 1));
        Log.Out("[Parachute] Gave " + name + " Q" + quality + ".");
    }

    private static void GiveKit(EntityPlayerLocal player)
    {
        ItemValue prototype = ItemClass.GetItem(DefaultOutfit, false);
        if (prototype == null || prototype.IsEmpty())
        {
            Log.Out("[Parachute] Unknown item: " + DefaultOutfit);
            return;
        }
        ItemValue mod = ItemClass.GetItem(ModItem, false);
        if (mod == null || mod.IsEmpty())
        {
            Log.Out("[Parachute] " + ModItem + " is not loaded.");
            return;
        }
        ItemValue outfit = new ItemValue(prototype.type, 6, 6, false);
        bool installed = false;
        for (int i = 0; i < outfit.ModificationCount; i++)
        {
            ItemValue slot = outfit.GetModification(i);
            if (slot == null || slot.IsEmpty())
            {
                outfit.SetModification(i, new ItemValue(mod.type));
                installed = true;
                break;
            }
        }
        if (!installed)
        {
            Log.Out("[Parachute] No free mod slot on " + DefaultOutfit + " Q6; giving the mod loose instead.");
            GiveItem(player, ModItem, 1);
        }
        GiveStack(player, new ItemStack(outfit, 1));
        Log.Out("[Parachute] Gave " + DefaultOutfit + " Q6" + (installed ? " with the parachute mod installed." : "."));
    }

    private static void GiveItem(EntityPlayerLocal player, string name, int count)
    {
        ItemValue value = ItemClass.GetItem(name, false);
        if (value == null || value.IsEmpty())
        {
            Log.Out("[Parachute] Item unavailable after XML load: " + name);
            return;
        }
        int remaining = count;
        int given = 0;
        while (remaining > 0)
        {
            int batch = Math.Min(remaining, 150);
            ItemStack stack = new ItemStack(value, batch);
            if ((player.bag != null && player.bag.AddItem(stack)) ||
                (player.inventory != null && player.inventory.AddItem(stack)))
            {
                given += batch;
                remaining -= batch;
                continue;
            }
            break;
        }
        if (remaining > 0)
        {
            GameManager.Instance.ItemDropServer(new ItemStack(value, remaining), player.GetPosition(),
                Vector3.zero, player.entityId, 60f, false);
            Log.Out("[Parachute] Backpack full; dropped " + remaining + " " + name + ".");
        }
        if (given > 0) Log.Out("[Parachute] Gave " + given + " " + name + ".");
    }

    private static void GiveStack(EntityPlayerLocal player, ItemStack stack)
    {
        if ((player.bag != null && player.bag.AddItem(stack)) ||
            (player.inventory != null && player.inventory.AddItem(stack)))
            return;
        if (stack.count > 0)
            GameManager.Instance.ItemDropServer(stack, player.GetPosition(), Vector3.zero, player.entityId, 60f, false);
    }

    private static void Up(List<string> args, EntityPlayerLocal player)
    {
        float meters = 60f;
        if (args.Count > 1 && (!float.TryParse(args[1], out meters) || meters < 1f || meters > 500f))
        {
            Log.Out("[Parachute] Height must be 1-500 m.");
            return;
        }
        player.TeleportToPosition(player.GetPosition() + Vector3.up * meters);
        Log.Out("[Parachute] Moved up " + meters.ToString("F0") + " m.");
    }

    private static void Status(EntityPlayerLocal player)
    {
        ParachuteConfig config = ParachuteMod.Settings;
        Log.Out("[Parachute] state=" + ParachuteState.Phase +
            " harness=" + player.Buffs.HasBuff("buffParachuteHarness") +
            " deployed=" + player.Buffs.HasBuff("buffParachuteDeployed") +
            " descent=" + ParachuteState.CurrentFallSpeed.ToString("F1") + " m/s" +
            " glide=" + ParachuteState.CurrentGlideSpeed.ToString("F1") + " m/s");
        Log.Out("[Parachute] fall_speed=" + config.FallSpeed + " glide_speed=" + config.GlideSpeed +
            " left_right=" + config.LeftRight + " strafe_speed=" + config.StrafeSpeed + " turn_rate=" + config.TurnRate + " deploy_time=" + config.DeployTime +
            " input=" + config.DeployInput + " key=" + config.DeployKey + " debug=" + config.Debug);
    }

    private static void Debug(List<string> args)
    {
        bool on = true;
        if (args.Count > 1)
        {
            string value = args[1].ToLowerInvariant();
            if (value == "off" || value == "false" || value == "0") on = false;
        }
        ParachuteMod.Settings.Debug = on;
        Log.Out("[Parachute] Debug " + (on ? "on" : "off") + ".");
    }

    private static int Count(List<string> args, int index, int fallback)
    {
        if (args.Count <= index) return fallback;
        if (!int.TryParse(args[index], out int count) || count < 1 || count > 1000)
        {
            Log.Out("[Parachute] Count must be 1-1000.");
            return fallback;
        }
        return count;
    }

    private static readonly FastTags<TagGroup.Global> ArmorChestTag = FastTags<TagGroup.Global>.Parse("armorChest");
}
