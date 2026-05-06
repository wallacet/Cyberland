using Cyberland.Demo.IdleGold.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Localization;

namespace Cyberland.Demo.IdleGold;

// UI-gap: typed UiCommands / localization format helpers would trim manual string composition here.

/// <summary>Applies purchase commands to the session row and records localized log lines.</summary>
public static class UiCommandHandler
{
    public static void Dispatch(World world, EntityId session, LocalizationManager loc, object? cmd)
    {
        if (cmd is not UiGameCommand gameCmd)
            return;

        switch (gameCmd)
        {
            case UnlockSourceCommand u:
                TryUnlock(world, session, loc, u.Source);
                break;
            case LevelSourceCommand l:
                TryLevel(world, session, loc, l.Source);
                break;
            case BuyStatCommand b:
                TryBuyStat(world, session, loc, b.Stat);
                break;
            case UpgradeEquipmentCommand e:
                TryUpgradeEquip(world, session, loc, e.Slot);
                break;
        }
    }

    private static void TryUnlock(World world, EntityId session, LocalizationManager loc, SourceId id)
    {
        ref var wallet = ref world.Get<Wallet>(session);
        ref var sources = ref world.Get<Sources>(session);
        ref var row = ref GameBalance.Row(ref sources, id);

        if (row.Unlocked)
            return;

        var cost = GameBalance.UnlockCost(id);
        if (wallet.Gold < cost)
            return;

        wallet.Gold -= cost;
        row.Unlocked = true;
        row.Level = 1;

        var name = loc.Get(SourceNameKey(id));
        LogBook.Append(world, session, $"{loc.Get("idlegold.log.unlocked")} {name}");
    }

    private static void TryLevel(World world, EntityId session, LocalizationManager loc, SourceId id)
    {
        ref var wallet = ref world.Get<Wallet>(session);
        ref var sources = ref world.Get<Sources>(session);
        ref var row = ref GameBalance.Row(ref sources, id);

        if (!row.Unlocked)
            return;

        var cost = Economy.LevelUpCost(id, row.Level);
        if (wallet.Gold < cost)
            return;

        wallet.Gold -= cost;
        row.Level++;

        var name = loc.Get(SourceNameKey(id));
        LogBook.Append(world, session, $"{loc.Get("idlegold.log.leveled")} {name} → L{row.Level}");
    }

    private static void TryBuyStat(World world, EntityId session, LocalizationManager loc, StatKind stat)
    {
        ref var wallet = ref world.Get<Wallet>(session);
        ref var stats = ref world.Get<Stats>(session);

        ref var slot = ref StatSlot(ref stats, stat);

        var next = slot + 1;
        var cost = Economy.StatPurchaseCost(next);
        if (wallet.Gold < cost)
            return;

        wallet.Gold -= cost;
        slot = next;

        var name = loc.Get(StatNameKey(stat));
        LogBook.Append(world, session, $"{loc.Get("idlegold.log.stat_up")} {name} → L{slot}");
    }

    private static ref int StatSlot(ref Stats stats, StatKind stat)
    {
        switch (stat)
        {
            case StatKind.Might:
                return ref stats.Might;
            case StatKind.Cunning:
                return ref stats.Cunning;
            case StatKind.Resolve:
                return ref stats.Resolve;
            case StatKind.Luck:
                return ref stats.Luck;
            default:
                return ref stats.Might;
        }
    }

    private static void TryUpgradeEquip(World world, EntityId session, LocalizationManager loc, EquipSlot slot)
    {
        ref var wallet = ref world.Get<Wallet>(session);
        ref var eq = ref world.Get<Equipment>(session);
        ref var tier = ref GameBalance.Tier(ref eq, slot);

        if (tier >= GameBalance.TierCount - 1)
            return;

        var next = tier + 1;
        var cost = Economy.SlotUpgradeCost(slot, next);
        if (wallet.Gold < cost)
            return;

        wallet.Gold -= cost;
        tier = next;

        var slotName = loc.Get(SlotNameKey(slot));
        var tierName = loc.Get(GameBalance.TierLocalizationKey(tier));
        LogBook.Append(world, session, $"{loc.Get("idlegold.log.equip_up")} {slotName}: {tierName}");
    }

    private static string SourceNameKey(SourceId id) =>
        id switch
        {
            SourceId.VillageBeg => "idlegold.source.village.name",
            SourceId.ForestForage => "idlegold.source.forest.name",
            SourceId.CaveExplore => "idlegold.source.cave.name",
            SourceId.RoadToll => "idlegold.source.road.name",
            _ => "idlegold.source.village.name"
        };

    private static string StatNameKey(StatKind k) =>
        k switch
        {
            StatKind.Might => "idlegold.stat.might",
            StatKind.Cunning => "idlegold.stat.cunning",
            StatKind.Resolve => "idlegold.stat.resolve",
            StatKind.Luck => "idlegold.stat.luck",
            _ => "idlegold.stat.might"
        };

    private static string SlotNameKey(EquipSlot s) =>
        s switch
        {
            EquipSlot.Weapon => "idlegold.slot.weapon",
            EquipSlot.Helm => "idlegold.slot.helm",
            EquipSlot.Chest => "idlegold.slot.chest",
            EquipSlot.Boots => "idlegold.slot.boots",
            EquipSlot.Ring => "idlegold.slot.ring",
            _ => "idlegold.slot.weapon"
        };
}
