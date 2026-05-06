using Cyberland.Demo.IdleGold.Components;
using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Core;
using Silk.NET.Maths;

namespace Cyberland.Demo.IdleGold.Systems;

/// <summary>Pushes session state into <see cref="DocumentRefs"/> before <see cref="Cyberland.Engine.Scene.Systems.UiDocumentFrameSystem"/> lays out and draws.</summary>
[RunBefore("cyberland.engine/ui-document-frame")]
public sealed class HudBindSystem : ISingletonSystem, ISingletonLateUpdate
{
    private static readonly SourceId[] SourceOrder =
    [
        SourceId.VillageBeg,
        SourceId.ForestForage,
        SourceId.CaveExplore,
        SourceId.RoadToll
    ];

    private static readonly StatKind[] StatOrder =
        [StatKind.Might, StatKind.Cunning, StatKind.Resolve, StatKind.Luck];

    private static readonly EquipSlot[] EquipOrder =
        [EquipSlot.Weapon, EquipSlot.Helm, EquipSlot.Chest, EquipSlot.Boots, EquipSlot.Ring];

    private static readonly Vector4D<float> DetailUnlockedColor = new(0.52f, 0.88f, 0.96f, 1f);
    private static readonly Vector4D<float> DetailLockedColor = new(0.92f, 0.68f, 0.38f, 1f);

    private static readonly Vector4D<float> BtnCaptionBright = new(0.97f, 0.98f, 1f, 1f);
    private static readonly Vector4D<float> BtnCaptionMuted = new(0.62f, 0.66f, 0.74f, 0.92f);

    private readonly DocumentRefs _refs;
    private readonly LocalizationManager _loc;
    private readonly GameHostServices _host;
    private readonly FpsMovingAverage _fpsAverage = new(FpsMovingAverage.DefaultWindowSeconds);

    public HudBindSystem(DocumentRefs refs, LocalizationManager loc, GameHostServices host)
    {
        _refs = refs;
        _loc = loc;
        _host = host;
    }

    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SessionTag>();

    public void OnSingletonStart(in SingletonEntity stateRow)
    {
        _ = ref stateRow.Get<Wallet>();
    }

    public void OnSingletonLateUpdate(in SingletonEntity row, float deltaSeconds)
    {
        ref var wallet = ref row.Get<Wallet>();
        ref var sources = ref row.Get<Sources>();
        ref var stats = ref row.Get<Stats>();
        ref var eq = ref row.Get<Equipment>();

        var rate = Economy.TotalGoldPerSecond(ref sources, in stats, in eq);
        _refs.ChromeGold.Text = $"{wallet.Gold:F2}";
        _refs.ChromeGold.InvalidateLayout();
        _refs.ChromeGps.Text = $"{rate:F2} {_loc.Get("idlegold.ui.gold_per_sec")}";
        _refs.ChromeGps.InvalidateLayout();

        BindSources(ref wallet, ref sources, ref stats, ref eq);
        BindStats(ref wallet, ref stats, ref sources, ref eq);
        BindEquipment(ref wallet, ref eq);
        BindLog(row.World, row.Entity);

        if (_refs.HasFpsHud)
        {
            var frameSeconds = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
            _fpsAverage.AddFrameDeltaSeconds(frameSeconds);
            var label = _fpsAverage.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS —";
            ref var hud = ref row.World.Get<BitmapText>(_refs.FpsHudEntity);
            hud.Content = label;
            hud.Visible = true;
        }
    }

    private void BindSources(ref Wallet wallet, ref Sources sources, ref Stats stats, ref Equipment eq)
    {
        for (var i = 0; i < SourceOrder.Length; i++)
        {
            var id = SourceOrder[i];
            var card = _refs.SourceCards[i];
            ref var row = ref GameBalance.Row(ref sources, id);

            card.NameText.Text = _loc.Get(SourceTitleKey(id));
            card.DescText.Text = _loc.Get(SourceDescKey(id));

            var eff = Economy.EffectiveRate(id, ref sources, in stats, in eq);
            card.DetailText.Text = row.Unlocked
                ? $"{_loc.Get("idlegold.ui.level")} {row.Level} · {_loc.Get("idlegold.ui.rate")} {eff:F2}/s"
                : _loc.Get("idlegold.ui.locked");
            card.DetailText.DefaultStyle = new TextStyle(
                BuiltinFonts.UiSans,
                14f,
                row.Unlocked ? DetailUnlockedColor : DetailLockedColor);
            card.DetailText.InvalidateLayout();

            var unlockCost = GameBalance.UnlockCost(id);
            card.UnlockButton.Visible = !row.Unlocked;
            var unlockAffordable = wallet.Gold >= unlockCost && id != SourceId.VillageBeg;
            card.UnlockButton.Interactable = unlockAffordable;
            card.UnlockCaption.Text.Text =
                id == SourceId.VillageBeg ? _loc.Get("idlegold.ui.always_on") : $"{_loc.Get("idlegold.ui.unlock")} ({unlockCost:F0})";
            card.UnlockCaption.Text.DefaultStyle = new TextStyle(
                BuiltinFonts.UiSans,
                14f,
                unlockAffordable ? BtnCaptionBright : BtnCaptionMuted,
                Bold: true);
            card.UnlockCaption.Text.InvalidateLayout();

            var levelCost = Economy.LevelUpCost(id, row.Level);
            card.LevelButton.Visible = row.Unlocked;
            var levelAffordable = wallet.Gold >= levelCost;
            card.LevelButton.Interactable = levelAffordable;
            card.LevelCaption.Text.Text = $"{_loc.Get("idlegold.ui.level_up")} ({levelCost:F0})";
            card.LevelCaption.Text.DefaultStyle = new TextStyle(
                BuiltinFonts.UiSans,
                14f,
                levelAffordable ? BtnCaptionBright : BtnCaptionMuted,
                Bold: true);
            card.LevelCaption.Text.InvalidateLayout();
        }
    }

    private void BindStats(ref Wallet wallet, ref Stats stats, ref Sources sources, ref Equipment eq)
    {
        for (var i = 0; i < StatOrder.Length; i++)
        {
            var kind = StatOrder[i];
            var ui = _refs.StatRows[i];
            var level = StatLevel(in stats, kind);
            var mult = Economy.StatMultiplier(kind, in stats);
            var globalRate = Economy.TotalGoldPerSecond(ref sources, in stats, in eq);
            ui.Summary.Text =
                $"{StatTitle(kind)} L{level} · ×{mult:F2} · {_loc.Get("idlegold.ui.total_rate")} {globalRate:F2}/s";

            var nextLevel = level + 1;
            var cost = Economy.StatPurchaseCost(nextLevel);
            var trainAffordable = wallet.Gold >= cost;
            ui.BuyButton.Interactable = trainAffordable;
            ui.BuyCaption.Text.Text = $"{_loc.Get("idlegold.ui.train")} ({cost:F0})";
            ui.BuyCaption.Text.DefaultStyle = new TextStyle(
                BuiltinFonts.UiSans,
                14f,
                trainAffordable ? BtnCaptionBright : BtnCaptionMuted,
                Bold: true);
        }
    }

    private string StatTitle(StatKind kind) =>
        kind switch
        {
            StatKind.Might => _loc.Get("idlegold.stat.might"),
            StatKind.Cunning => _loc.Get("idlegold.stat.cunning"),
            StatKind.Resolve => _loc.Get("idlegold.stat.resolve"),
            StatKind.Luck => _loc.Get("idlegold.stat.luck"),
            _ => ""
        };

    private static int StatLevel(in Stats s, StatKind k) =>
        k switch
        {
            StatKind.Might => s.Might,
            StatKind.Cunning => s.Cunning,
            StatKind.Resolve => s.Resolve,
            StatKind.Luck => s.Luck,
            _ => 0
        };

    private void BindEquipment(ref Wallet wallet, ref Equipment eq)
    {
        for (var i = 0; i < EquipOrder.Length; i++)
        {
            var slot = EquipOrder[i];
            var cell = _refs.EquipCells[i];
            var tier = GameBalance.Tier(ref eq, slot);
            cell.SlotText.Text = SlotTitle(slot);
            cell.SlotText.InvalidateLayout();
            cell.TierText.Text = _loc.Get(GameBalance.TierLocalizationKey(tier));
            cell.TierText.InvalidateLayout();
            cell.Icon.Tint = TierVisual.Stripe(tier);

            var maxed = tier >= GameBalance.TierCount - 1;
            var nextTier = tier + 1;
            var cost = maxed ? 0d : Economy.SlotUpgradeCost(slot, nextTier);
            var upgradeAffordable = !maxed && wallet.Gold >= cost;
            cell.UpgradeButton.Interactable = upgradeAffordable;
            cell.UpgradeCaption.Text.Text =
                maxed ? _loc.Get("idlegold.ui.max_tier") : $"{_loc.Get("idlegold.ui.upgrade")} ({cost:F0})";
            cell.UpgradeCaption.Text.DefaultStyle = new TextStyle(
                BuiltinFonts.UiSans,
                14f,
                maxed ? BtnCaptionMuted : upgradeAffordable ? BtnCaptionBright : BtnCaptionMuted,
                Bold: true);
            cell.UpgradeCaption.Text.InvalidateLayout();
        }
    }

    private string SlotTitle(EquipSlot slot) =>
        slot switch
        {
            EquipSlot.Weapon => _loc.Get("idlegold.slot.weapon"),
            EquipSlot.Helm => _loc.Get("idlegold.slot.helm"),
            EquipSlot.Chest => _loc.Get("idlegold.slot.chest"),
            EquipSlot.Boots => _loc.Get("idlegold.slot.boots"),
            EquipSlot.Ring => _loc.Get("idlegold.slot.ring"),
            _ => _loc.Get("idlegold.slot.weapon")
        };

    private void BindLog(World world, EntityId session)
    {
        _refs.LogBody.Text = LogBook.BuildText(world, session);
        _refs.LogScroll.ContentOffset = new Vector2D<float>(0f, 1e6f);
    }

    private static string SourceTitleKey(SourceId id) =>
        id switch
        {
            SourceId.VillageBeg => "idlegold.source.village.name",
            SourceId.ForestForage => "idlegold.source.forest.name",
            SourceId.CaveExplore => "idlegold.source.cave.name",
            SourceId.RoadToll => "idlegold.source.road.name",
            _ => ""
        };

    private static string SourceDescKey(SourceId id) =>
        id switch
        {
            SourceId.VillageBeg => "idlegold.source.village.desc",
            SourceId.ForestForage => "idlegold.source.forest.desc",
            SourceId.CaveExplore => "idlegold.source.cave.desc",
            SourceId.RoadToll => "idlegold.source.road.desc",
            _ => ""
        };
}
