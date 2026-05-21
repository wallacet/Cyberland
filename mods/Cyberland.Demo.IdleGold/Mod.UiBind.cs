using Cyberland.Demo.IdleGold.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.RuntimeUi;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Text;
using Silk.NET.Maths;
using System.Diagnostics;

namespace Cyberland.Demo.IdleGold;

public sealed partial class Mod
{
    private static SceneBootstrap BootstrapUi(ModLoadContext context, EntityId sessionEntity)
    {
        var refs = ResolveDocumentRefs(context);
        WireTabs(refs);
        WirePurchases(context.Host, refs);
        return new SceneBootstrap(refs, sessionEntity);
    }

    private static DocumentRefs ResolveDocumentRefs(ModLoadContext context)
    {
        var world = context.World;
        var hudRoot = world.RequireSingleEntityWith<HudRootTag>("IdleGold HUD root");
        if (!context.Host.UiDocuments.TryGetElements(hudRoot, out var ids))
            throw new InvalidOperationException("IdleGold HUD document was not attached from JSON.");

        var navGather = ids.Require<UiRadioButton>("nav.gather");
        navGather.Group.Select(NavGather);

        return new DocumentRefs
        {
            NavGroup = navGather.Group,
            GatherPanel = ids.Require<UiPanel>("gather.panel"),
            CharacterPanel = ids.Require<UiPanel>("character.panel"),
            BlacksmithPanel = ids.Require<UiPanel>("blacksmith.panel"),
            LogPanel = ids.Require<UiPanel>("log.panel"),
            ChromeGold = ids.Require<UiTextBlock>("chrome.gold"),
            ChromeGps = ids.Require<UiTextBlock>("chrome.gps"),
            ChromeFps = ids.Require<UiTextBlock>("chrome.fps"),
            SourceCards =
            [
                ResolveSourceCard(ids, 0),
                ResolveSourceCard(ids, 1),
                ResolveSourceCard(ids, 2),
                ResolveSourceCard(ids, 3)
            ],
            StatRows =
            [
                ResolveStatRow(ids, 0),
                ResolveStatRow(ids, 1),
                ResolveStatRow(ids, 2),
                ResolveStatRow(ids, 3)
            ],
            EquipCells =
            [
                ResolveEquipCell(ids, 0),
                ResolveEquipCell(ids, 1),
                ResolveEquipCell(ids, 2),
                ResolveEquipCell(ids, 3),
                ResolveEquipCell(ids, 4)
            ],
            LogScroll = ids.Require<UiScrollView>("log.scroll"),
            LogBody = ids.Require<UiTextBlock>("log.body")
        };
    }

    private static SourceCardRefs ResolveSourceCard(IReadOnlyDictionary<string, UiElement> ids, int index)
    {
        var p = $"{index}";
        return new SourceCardRefs
        {
            Stripe = ids.Require<UiImage>($"source.{p}.stripe"),
            NameText = ids.Require<UiTextBlock>($"source.{p}.name"),
            DescText = ids.Require<UiTextBlock>($"source.{p}.desc"),
            DetailText = ids.Require<UiTextBlock>($"source.{p}.detail"),
            UnlockButton = ids.Require<UiButton>($"source.{p}.unlock"),
            UnlockCaption = ids.Require<UiLabel>($"source.{p}.unlock.caption"),
            LevelButton = ids.Require<UiButton>($"source.{p}.level"),
            LevelCaption = ids.Require<UiLabel>($"source.{p}.level.caption")
        };
    }

    private static StatRowRefs ResolveStatRow(IReadOnlyDictionary<string, UiElement> ids, int index) =>
        new()
        {
            Summary = ids.Require<UiTextBlock>($"stat.{index}.summary"),
            BuyButton = ids.Require<UiButton>($"stat.{index}.buy"),
            BuyCaption = ids.Require<UiLabel>($"stat.{index}.buy.caption")
        };

    private static EquipCellRefs ResolveEquipCell(IReadOnlyDictionary<string, UiElement> ids, int index) =>
        new()
        {
            Icon = ids.Require<UiImage>($"equip.{index}.icon"),
            SlotText = ids.Require<UiTextBlock>($"equip.{index}.slot"),
            TierText = ids.Require<UiTextBlock>($"equip.{index}.tier"),
            UpgradeButton = ids.Require<UiButton>($"equip.{index}.upgrade"),
            UpgradeCaption = ids.Require<UiLabel>($"equip.{index}.upgrade.caption")
        };

    private static void WireTabs(DocumentRefs refs)
    {
        refs.CurrentTabId = NavGather;
        var switchTimer = Stopwatch.StartNew();
        var loggedTabs = new HashSet<string>(StringComparer.Ordinal);
        refs.NavGroup.SelectionChanged += (_, id) =>
        {
            if (string.IsNullOrWhiteSpace(id))
                return;
            refs.CurrentTabId = id;
            refs.GatherPanel.Visible = id == NavGather;
            refs.CharacterPanel.Visible = id == NavCharacter;
            refs.BlacksmithPanel.Visible = id == NavBlacksmith;
            refs.LogPanel.Visible = id == NavLog;
            if (loggedTabs.Add(id))
            {
                Console.WriteLine(
                    $"IdleGold tab first-select | id={id} elapsed_ms={switchTimer.Elapsed.TotalMilliseconds:0.###}");
            }
        };
    }

    private static void WirePurchases(GameHostServices host, DocumentRefs refs)
    {
        for (var i = 0; i < 4; i++)
        {
            var sid = (SourceId)i;
            refs.SourceCards[i].UnlockButton.Clicked += (_, _) => host.UiCommands.Enqueue(new UnlockSourceCommand(sid));
            refs.SourceCards[i].LevelButton.Clicked += (_, _) => host.UiCommands.Enqueue(new LevelSourceCommand(sid));
        }

        for (var i = 0; i < 4; i++)
        {
            var sk = (StatKind)i;
            refs.StatRows[i].BuyButton.Clicked += (_, _) => host.UiCommands.Enqueue(new BuyStatCommand(sk));
        }

        for (var i = 0; i < 5; i++)
        {
            var slot = (EquipSlot)i;
            refs.EquipCells[i].UpgradeButton.Clicked += (_, _) =>
                host.UiCommands.Enqueue(new UpgradeEquipmentCommand(slot));
        }
    }
}
