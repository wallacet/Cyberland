using Cyberland.Demo.IdleGold.Components;

namespace Cyberland.Demo.IdleGold;

/// <summary>Tunable economy constants; kept in one place per design doc §11.</summary>
public static class GameBalance
{
    public const int TierCount = 8;

    /// <summary>Per-tier global income multiplier (multiplicative across slots).</summary>
    public static readonly float[] TierIncomeMultiplier =
    [
        1f, 1.12f, 1.28f, 1.48f, 1.72f, 2f, 2.35f, 2.75f
    ];

    public const float LevelCostBase = 12f;
    public const float LevelCostGrowth = 1.55f;

    public const float StatCostBase = 25f;
    public const float StatCostLinear = 18f;

    public const float StatMultPerLevel = 0.06f;
    public const float LuckGlobalMultPerLevel = 0.01f;

    public const float LuckProcIntervalSec = 2.5f;
    public const float LuckProcChanceBase = 0.06f;
    public const float LuckProcChancePerLuck = 0.025f;
    public const float LuckProcChanceCap = 0.38f;
    public const double LuckProcBonusGoldBase = 10d;
    public const double LuckProcBonusPerLuck = 3d;

    public static double UnlockCost(SourceId id) =>
        id switch
        {
            SourceId.VillageBeg => 0d,
            SourceId.ForestForage => 75d,
            SourceId.CaveExplore => 320d,
            SourceId.RoadToll => 1200d,
            _ => 0d
        };

    public static double BaseRate(SourceId id) =>
        id switch
        {
            SourceId.VillageBeg => 2.5d,
            SourceId.ForestForage => 5d,
            SourceId.CaveExplore => 11d,
            SourceId.RoadToll => 26d,
            _ => 0d
        };

    public static double SlotUpgradeBaseGold(EquipSlot slot) =>
        slot switch
        {
            EquipSlot.Weapon => 90d,
            EquipSlot.Helm => 55d,
            EquipSlot.Chest => 70d,
            EquipSlot.Boots => 50d,
            EquipSlot.Ring => 40d,
            _ => 50d
        };

    public const float SlotUpgradeGrowth = 1.62f;

    public static ref SourceRow Row(ref Sources s, SourceId id)
    {
        switch (id)
        {
            case SourceId.VillageBeg:
                return ref s.VillageBeg;
            case SourceId.ForestForage:
                return ref s.ForestForage;
            case SourceId.CaveExplore:
                return ref s.CaveExplore;
            case SourceId.RoadToll:
                return ref s.RoadToll;
            default:
                return ref s.VillageBeg;
        }
    }

    public static ref int Tier(ref Equipment e, EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Weapon:
                return ref e.WeaponTier;
            case EquipSlot.Helm:
                return ref e.HelmTier;
            case EquipSlot.Chest:
                return ref e.ChestTier;
            case EquipSlot.Boots:
                return ref e.BootsTier;
            case EquipSlot.Ring:
                return ref e.RingTier;
            default:
                return ref e.WeaponTier;
        }
    }

    public static string TierLocalizationKey(int tierIndex) =>
        $"idlegold.tier.{tierIndex}";
}
