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
using System.Linq;

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
    private static readonly TextStyle DetailUnlockedStyle = new(BuiltinFonts.UiSans, 14f, DetailUnlockedColor);
    private static readonly TextStyle DetailLockedStyle = new(BuiltinFonts.UiSans, 14f, DetailLockedColor);
    private static readonly TextStyle ButtonCaptionBrightStyle = new(BuiltinFonts.UiSans, 14f, BtnCaptionBright, Bold: true);
    private static readonly TextStyle ButtonCaptionMutedStyle = new(BuiltinFonts.UiSans, 14f, BtnCaptionMuted, Bold: true);

    private readonly DocumentRefs _refs;
    private readonly LocalizationManager _loc;
    private readonly GameHostServices _host;
    private readonly FpsMovingAverage _fpsAverage = new(FpsMovingAverage.DefaultWindowSeconds);

    /// <summary>Last chrome strings actually assigned — avoids <see cref="UiTextBlock"/> layout invalidation when raw
    /// wallet/sim values jitter more often than the formatted HUD changes.</summary>
    private string _lastChromeGoldDisplay = "";

    private string _lastChromeGpsDisplay = "";
    private double _lastChromeGoldValue = double.NaN;
    private double _lastChromeGpsValue = double.NaN;
    private int _lastLogRevision = -1;

    private float _fpsHudTimer;
    private float _chromeHudTimer;
    private string _lastFpsLabel = "";

    private readonly int[] _lastSourceDetailLevel = Enumerable.Repeat(-1, SourceOrder.Length).ToArray();
    private readonly bool[] _lastSourceDetailUnlocked = new bool[SourceOrder.Length];
    private readonly double[] _lastSourceDetailEff = new double[SourceOrder.Length];

    private readonly double[] _lastUnlockCost = Enumerable.Repeat(double.NaN, SourceOrder.Length).ToArray();
    private readonly bool[] _lastUnlockAffordable = new bool[SourceOrder.Length];

    private readonly double[] _lastLevelCost = Enumerable.Repeat(double.NaN, SourceOrder.Length).ToArray();
    private readonly bool[] _lastLevelAffordable = new bool[SourceOrder.Length];

    private double _lastStatSummaryRate = double.NaN;
    private readonly int[] _lastStatRowLevel = Enumerable.Repeat(-1, StatOrder.Length).ToArray();
    private readonly double[] _lastStatRowMult = Enumerable.Repeat(double.NaN, StatOrder.Length).ToArray();

    private readonly double[] _lastTrainCost = Enumerable.Repeat(double.NaN, StatOrder.Length).ToArray();
    private readonly bool[] _lastTrainAffordable = new bool[StatOrder.Length];

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

        var globalRate = Economy.TotalGoldPerSecond(ref sources, in stats, in eq);

        _chromeHudTimer += deltaSeconds;
        if (_chromeHudTimer >= 0.1f || _lastChromeGoldDisplay.Length == 0 || _lastChromeGpsDisplay.Length == 0)
        {
            _chromeHudTimer = 0f;
            if (Math.Abs(wallet.Gold - _lastChromeGoldValue) >= 0.01d || _lastChromeGoldDisplay.Length == 0)
            {
                _lastChromeGoldValue = wallet.Gold;
                var goldDisplay = $"{wallet.Gold:F2}";
                _lastChromeGoldDisplay = goldDisplay;
                _refs.ChromeGold.Text = goldDisplay;
            }

            if (Math.Abs(globalRate - _lastChromeGpsValue) >= 0.01d || _lastChromeGpsDisplay.Length == 0)
            {
                _lastChromeGpsValue = globalRate;
                var gpsDisplay = $"{globalRate:F2} {_loc.Get("idlegold.ui.gold_per_sec")}";
                _lastChromeGpsDisplay = gpsDisplay;
                _refs.ChromeGps.Text = gpsDisplay;
            }
        }

        BindSources(ref wallet, ref sources, ref stats, ref eq);
        BindStats(ref wallet, ref stats, ref sources, ref eq, globalRate);
        BindEquipment(ref wallet, ref eq);
        BindLog(row.World, row.Entity);

        if (_refs.HasFpsHud)
        {
            var frameSeconds = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
            _fpsAverage.AddFrameDeltaSeconds(frameSeconds);
            _fpsHudTimer += deltaSeconds;

            ref var hud = ref row.World.Get<BitmapText>(_refs.FpsHudEntity);
            hud.Visible = true;

            var label = _fpsAverage.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS —";
            if (_fpsHudTimer >= 0.25f || _lastFpsLabel.Length == 0)
            {
                if (_fpsHudTimer >= 0.25f)
                    _fpsHudTimer = 0f;
                if (label != _lastFpsLabel)
                {
                    _lastFpsLabel = label;
                    hud.Content = label;
                }
            }
        }
    }

    private void BindSources(ref Wallet wallet, ref Sources sources, ref Stats stats, ref Equipment eq)
    {
        for (var i = 0; i < SourceOrder.Length; i++)
        {
            var id = SourceOrder[i];
            var card = _refs.SourceCards[i];
            ref var row = ref GameBalance.Row(ref sources, id);

            var name = _loc.Get(SourceTitleKey(id));
            if (card.NameText.Text != name)
                card.NameText.Text = name;

            var desc = _loc.Get(SourceDescKey(id));
            if (card.DescText.Text != desc)
                card.DescText.Text = desc;

            var eff = Economy.EffectiveRate(id, ref sources, in stats, in eq);
            var detailChanged = _lastSourceDetailLevel[i] != row.Level ||
                                _lastSourceDetailUnlocked[i] != row.Unlocked ||
                                Math.Abs(_lastSourceDetailEff[i] - eff) > 1e-9;
            if (detailChanged)
            {
                _lastSourceDetailLevel[i] = row.Level;
                _lastSourceDetailUnlocked[i] = row.Unlocked;
                _lastSourceDetailEff[i] = eff;
                card.DetailText.Text = row.Unlocked
                    ? $"{_loc.Get("idlegold.ui.level")} {row.Level} · {_loc.Get("idlegold.ui.rate")} {eff:F2}/s"
                    : _loc.Get("idlegold.ui.locked");
            }

            var detailStyle = row.Unlocked ? DetailUnlockedStyle : DetailLockedStyle;
            if (!detailStyle.Equals(card.DetailText.DefaultStyle))
                card.DetailText.DefaultStyle = detailStyle;

            var unlockCost = GameBalance.UnlockCost(id);
            card.UnlockButton.Visible = !row.Unlocked;
            var unlockAffordable = wallet.Gold >= unlockCost && id != SourceId.VillageBeg;
            card.UnlockButton.Interactable = unlockAffordable;
            var unlockCapChanged = Math.Abs(_lastUnlockCost[i] - unlockCost) > 1e-9 ||
                                   _lastUnlockAffordable[i] != unlockAffordable;
            if (unlockCapChanged)
            {
                _lastUnlockCost[i] = unlockCost;
                _lastUnlockAffordable[i] = unlockAffordable;
                card.UnlockCaption.Text.Text = id == SourceId.VillageBeg
                    ? _loc.Get("idlegold.ui.always_on")
                    : $"{_loc.Get("idlegold.ui.unlock")} ({unlockCost:F0})";
            }

            var unlockStyle = unlockAffordable ? ButtonCaptionBrightStyle : ButtonCaptionMutedStyle;
            if (!unlockStyle.Equals(card.UnlockCaption.Text.DefaultStyle))
                card.UnlockCaption.Text.DefaultStyle = unlockStyle;

            var levelCost = Economy.LevelUpCost(id, row.Level);
            card.LevelButton.Visible = row.Unlocked;
            var levelAffordable = wallet.Gold >= levelCost;
            card.LevelButton.Interactable = levelAffordable;
            var levelCapChanged = Math.Abs(_lastLevelCost[i] - levelCost) > 1e-9 ||
                                  _lastLevelAffordable[i] != levelAffordable;
            if (levelCapChanged)
            {
                _lastLevelCost[i] = levelCost;
                _lastLevelAffordable[i] = levelAffordable;
                card.LevelCaption.Text.Text = $"{_loc.Get("idlegold.ui.level_up")} ({levelCost:F0})";
            }

            var levelStyle = levelAffordable ? ButtonCaptionBrightStyle : ButtonCaptionMutedStyle;
            if (!levelStyle.Equals(card.LevelCaption.Text.DefaultStyle))
                card.LevelCaption.Text.DefaultStyle = levelStyle;
        }
    }

    private void BindStats(ref Wallet wallet, ref Stats stats, ref Sources sources, ref Equipment eq, double globalRate)
    {
        var rateMoved = Math.Abs(_lastStatSummaryRate - globalRate) > 1e-9;
        for (var i = 0; i < StatOrder.Length; i++)
        {
            var kind = StatOrder[i];
            var ui = _refs.StatRows[i];
            var level = StatLevel(in stats, kind);
            var mult = Economy.StatMultiplier(kind, in stats);
            var summaryChanged = rateMoved ||
                                 _lastStatRowLevel[i] != level ||
                                 Math.Abs(_lastStatRowMult[i] - mult) > 1e-9;
            if (summaryChanged)
            {
                _lastStatRowLevel[i] = level;
                _lastStatRowMult[i] = mult;
                ui.Summary.Text =
                    $"{StatTitle(kind)} L{level} · ×{mult:F2} · {_loc.Get("idlegold.ui.total_rate")} {globalRate:F2}/s";
            }

            var nextLevel = level + 1;
            var cost = Economy.StatPurchaseCost(nextLevel);
            var trainAffordable = wallet.Gold >= cost;
            ui.BuyButton.Interactable = trainAffordable;
            var trainCapChanged = Math.Abs(_lastTrainCost[i] - cost) > 1e-9 ||
                                  _lastTrainAffordable[i] != trainAffordable;
            if (trainCapChanged)
            {
                _lastTrainCost[i] = cost;
                _lastTrainAffordable[i] = trainAffordable;
                ui.BuyCaption.Text.Text = $"{_loc.Get("idlegold.ui.train")} ({cost:F0})";
            }

            var trainStyle = trainAffordable ? ButtonCaptionBrightStyle : ButtonCaptionMutedStyle;
            if (!trainStyle.Equals(ui.BuyCaption.Text.DefaultStyle))
                ui.BuyCaption.Text.DefaultStyle = trainStyle;
        }

        _lastStatSummaryRate = globalRate;
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
            var slotTitle = SlotTitle(slot);
            if (cell.SlotText.Text != slotTitle)
                cell.SlotText.Text = slotTitle;

            var tierLine = _loc.Get(GameBalance.TierLocalizationKey(tier));
            if (cell.TierText.Text != tierLine)
                cell.TierText.Text = tierLine;

            cell.Icon.Tint = TierVisual.Stripe(tier);

            var maxed = tier >= GameBalance.TierCount - 1;
            var nextTier = tier + 1;
            var cost = maxed ? 0d : Economy.SlotUpgradeCost(slot, nextTier);
            var upgradeAffordable = !maxed && wallet.Gold >= cost;
            cell.UpgradeButton.Interactable = upgradeAffordable;
            var upCap = maxed ? _loc.Get("idlegold.ui.max_tier") : $"{_loc.Get("idlegold.ui.upgrade")} ({cost:F0})";
            if (cell.UpgradeCaption.Text.Text != upCap)
                cell.UpgradeCaption.Text.Text = upCap;

            var upStyle = maxed
                ? ButtonCaptionMutedStyle
                : upgradeAffordable
                    ? ButtonCaptionBrightStyle
                    : ButtonCaptionMutedStyle;
            if (!upStyle.Equals(cell.UpgradeCaption.Text.DefaultStyle))
                cell.UpgradeCaption.Text.DefaultStyle = upStyle;
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
        var log = world.Get<EventLog>(session);
        if (log.ContentRevision == _lastLogRevision)
            return;

        _lastLogRevision = log.ContentRevision;
        _refs.LogBody.Text = LogBook.BuildText(world, session);
        _refs.LogScroll.VerticalOffset = 1e6f;
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
