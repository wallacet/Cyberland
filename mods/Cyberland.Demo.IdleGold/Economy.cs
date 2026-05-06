using Cyberland.Demo.IdleGold.Components;

namespace Cyberland.Demo.IdleGold;

/// <summary>Pure rate and cost math for the session row.</summary>
public static class Economy
{
    public static float StatMultiplier(StatKind kind, in Stats s) =>
        kind switch
        {
            StatKind.Might => 1f + GameBalance.StatMultPerLevel * s.Might,
            StatKind.Cunning => 1f + GameBalance.StatMultPerLevel * s.Cunning,
            StatKind.Resolve => 1f + GameBalance.StatMultPerLevel * s.Resolve,
            StatKind.Luck => 1f + GameBalance.LuckGlobalMultPerLevel * s.Luck,
            _ => 1f
        };

    public static float SourceStatMultiplier(SourceId source, in Stats s)
    {
        var might = StatMultiplier(StatKind.Might, in s);
        var cun = StatMultiplier(StatKind.Cunning, in s);
        var res = StatMultiplier(StatKind.Resolve, in s);
        var luck = StatMultiplier(StatKind.Luck, in s);

        var core = source switch
        {
            SourceId.VillageBeg => might,
            SourceId.ForestForage => cun,
            SourceId.CaveExplore => res,
            SourceId.RoadToll => (might + cun) * 0.5f,
            _ => 1f
        };

        return core * luck;
    }

    public static float EquipmentMultiplier(in Equipment e) =>
        TierMult(e.WeaponTier) * TierMult(e.HelmTier) * TierMult(e.ChestTier) * TierMult(e.BootsTier) *
        TierMult(e.RingTier);

    public static float TierMult(int tierIndex)
    {
        if (tierIndex < 0)
            tierIndex = 0;
        if (tierIndex >= GameBalance.TierIncomeMultiplier.Length)
            tierIndex = GameBalance.TierIncomeMultiplier.Length - 1;
        return GameBalance.TierIncomeMultiplier[tierIndex];
    }

    public static float LevelScalar(int level)
    {
        if (level < 1)
            level = 1;
        return 1f + 0.22f * (level - 1);
    }

    public static double LevelUpCost(SourceId id, int currentLevel)
    {
        if (currentLevel < 1)
            currentLevel = 1;
        return GameBalance.LevelCostBase * Math.Pow(GameBalance.LevelCostGrowth, currentLevel);
    }

    public static double StatPurchaseCost(int statLevelAfterPurchase)
    {
        var levelIndex = Math.Max(0, statLevelAfterPurchase - 1);
        return GameBalance.StatCostBase + GameBalance.StatCostLinear * levelIndex;
    }

    public static double SlotUpgradeCost(EquipSlot slot, int tierAfterUpgrade)
    {
        var idx = Math.Max(0, tierAfterUpgrade - 1);
        return GameBalance.SlotUpgradeBaseGold(slot) * Math.Pow(GameBalance.SlotUpgradeGrowth, idx);
    }

    public static double EffectiveRate(SourceId id, ref Sources src, in Stats stats, in Equipment eq)
    {
        ref var row = ref GameBalance.Row(ref src, id);
        if (!row.Unlocked)
            return 0d;

        var baseR = GameBalance.BaseRate(id);
        var scalar = LevelScalar(row.Level);
        var equip = EquipmentMultiplier(in eq);
        var st = SourceStatMultiplier(id, in stats);
        return baseR * scalar * equip * st;
    }

    public static double TotalGoldPerSecond(ref Sources src, in Stats stats, in Equipment eq)
    {
        double sum = 0;
        sum += EffectiveRate(SourceId.VillageBeg, ref src, in stats, in eq);
        sum += EffectiveRate(SourceId.ForestForage, ref src, in stats, in eq);
        sum += EffectiveRate(SourceId.CaveExplore, ref src, in stats, in eq);
        sum += EffectiveRate(SourceId.RoadToll, ref src, in stats, in eq);
        return sum;
    }
}
