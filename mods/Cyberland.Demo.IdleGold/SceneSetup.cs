using Cyberland.Demo.IdleGold.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Ecs;
using Cyberland.Engine.UI.Layout;
using Cyberland.Engine.UI.Text;
using Silk.NET.Maths;

namespace Cyberland.Demo.IdleGold;

/// <summary>Cold-start camera, session row, retained HUD, optional bitmap HUD, and global post tuning.</summary>
public static class SceneSetup
{
    public const string NavGather = "gather";
    public const string NavCharacter = "character";
    public const string NavBlacksmith = "blacksmith";
    public const string NavLog = "log";

    /// <summary>Builds scene entities and UI; registers the document on <see cref="Hosting.GameHostServices.UiDocuments"/>.</summary>
    public static async ValueTask<SceneBootstrap> SetupSceneAsync(ModLoadContext context,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var renderer = context.Host.Renderer;
        var world = context.World;
        var host = context.Host;
        var loc = context.LocalizedContent.Strings;

        SpawnCamera(world);

        var session = SpawnSession(world, loc);

        var (gatherPanel, sourceCards) = BuildGatherTab(renderer, loc);
        var (characterPanel, statRows) = BuildCharacterTab(loc);
        var (blacksmithPanel, equipCells) = BuildBlacksmithTab(renderer, loc);
        var (logPanel, logScroll, logBody) = BuildLogTab();

        var refs = BuildChromeAndShell(world, host, loc, gatherPanel, characterPanel, blacksmithPanel, logPanel,
            sourceCards, statRows, equipCells, logScroll, logBody);

        WireTabs(refs);
        WirePurchases(host, refs);

        ApplyGlobalPost(world);
        SpawnFpsHud(world, refs);

        await Task.CompletedTask.ConfigureAwait(false);
        return new SceneBootstrap(refs, session);
    }

    private static void SpawnCamera(World world)
    {
        var cameraEntity = world.CreateEntity();
        var xf = Transform.Identity;
        xf.WorldPosition = new Vector2D<float>(640f, 360f);
        world.GetOrAdd<Transform>(cameraEntity) = xf;
        world.GetOrAdd<Camera2D>(cameraEntity) = Camera2D.Create(new Vector2D<int>(1280, 720));
    }

    private static EntityId SpawnSession(World world, LocalizationManager loc)
    {
        var e = world.CreateEntity();
        world.GetOrAdd<SessionTag>(e);
        world.GetOrAdd<Wallet>(e) = new Wallet();
        world.GetOrAdd<Sources>(e) = new Sources
        {
            VillageBeg = new SourceRow { Unlocked = true, Level = 1 },
            ForestForage = default,
            CaveExplore = default,
            RoadToll = default
        };
        world.GetOrAdd<Stats>(e) = new Stats();
        world.GetOrAdd<Equipment>(e) = new Equipment();
        world.GetOrAdd<RngState>(e) = new RngState { State = 0xC0FFEE_DEAD_BEEFL };
        world.GetOrAdd<EventLog>(e) = new EventLog { Lines = new List<string>() };

        LogBook.Append(world, e, loc.Get("idlegold.log.welcome"));
        return e;
    }

    private static DocumentRefs BuildChromeAndShell(World world, GameHostServices host, LocalizationManager loc,
        UiPanel gatherPanel, UiPanel characterPanel, UiPanel blacksmithPanel, UiPanel logPanel,
        SourceCardRefs[] sourceCards, StatRowRefs[] statRows, EquipCellRefs[] equipCells,
        UiScrollView logScroll, UiTextBlock logBody)
    {
        var rootEntity = world.CreateEntity();
        world.GetOrAdd<UiDocumentRoot>(rootEntity) = new UiDocumentRoot
        {
            Visible = true,
            CoordinateSpace = CoordinateSpace.ViewportSpace,
            RootPreset = UiDocumentRootPreset.FullViewport,
            SortKeyBase = 860f
        };

        var doc = new UiDocument();
        var rootStack = new UiVerticalStack { Spacing = 4f, Margin = new UiThickness(14f, 14f, 14f, 14f) };
        UiLayoutPresets.StretchAll(rootStack);
        doc.Root.AddChild(rootStack);

        // Horizontal stacks measure children with infinite main-axis width; avoid StretchAll / TopStretch on text
        // that spans X inside them — measured width becomes ∞ and the whole document layout collapses.
        var chrome = new UiVerticalStack { Spacing = 8f };
        // Title → separator → chrome bar (~102px content + margins); band must not clip the wallet row.
        UiLayoutPresets.TopStretch(chrome, 108f);
        var title = TextBlock(loc.Get("idlegold.ui.title"), 23f, new Vector4D<float>(0.94f, 0.96f, 1f, 1f), bold: true);
        UiLayoutPresets.TopStretch(title, 30f);
        title.Margin = new UiThickness(0f, 0f, 0f, 10f);
        var chromeRule = new UiPanel { BackgroundColor = new Vector4D<float>(0.38f, 0.58f, 0.82f, 0.55f) };
        UiLayoutPresets.TopStretch(chromeRule, 2f);

        // Stretch cross-axis so nav + wallet share the same full-height slot (Center offset short rows differently).
        var chromeBar = new UiHorizontalStack { Spacing = 18f, CrossAlignment = UiCrossAlignment.Stretch };
        UiLayoutPresets.TopStretch(chromeBar, 44f);

        // Match radio pill height (38px) so wallet text centers on the same axis as nav labels.
        var statsRow = new UiHorizontalStack { Spacing = 14f, CrossAlignment = UiCrossAlignment.Center };
        UiLayoutPresets.TopStretch(statsRow, 44f);
        var gold = TextBlock("0", 22f, new Vector4D<float>(1f, 0.78f, 0.28f, 1f));
        UiLayoutPresets.TopLeftFixed(gold, 168f, 38f);
        gold.VerticalAlignment = UiTextVerticalAlignment.Center;
        var gps = TextBlock("0", 15f, new Vector4D<float>(0.58f, 0.82f, 0.62f, 1f));
        UiLayoutPresets.TopLeftFixed(gps, 260f, 38f);
        gps.VerticalAlignment = UiTextVerticalAlignment.Center;
        statsRow.AddChild(gold);
        statsRow.AddChild(gps);

        // Center pills vertically in the chrome band (Stretch would pin short tiles to the top edge).
        var nav = new UiHorizontalStack { Spacing = 10f, CrossAlignment = UiCrossAlignment.Center };
        UiLayoutPresets.TopStretch(nav, 44f);
        var navGroup = new UiRadioGroup();
        nav.AddChild(NavRadio(navGroup, NavGather, loc.Get("idlegold.nav.gather")));
        nav.AddChild(NavRadio(navGroup, NavCharacter, loc.Get("idlegold.nav.character")));
        nav.AddChild(NavRadio(navGroup, NavBlacksmith, loc.Get("idlegold.nav.blacksmith")));
        nav.AddChild(NavRadio(navGroup, NavLog, loc.Get("idlegold.nav.log")));

        chromeBar.AddChild(nav);
        chromeBar.AddChild(statsRow);
        chrome.AddChild(title);
        chrome.AddChild(chromeRule);
        chrome.AddChild(chromeBar);

        var bodyOuter = new UiPanel
        {
            Spacing = 0f,
            Margin = new UiThickness(0f, 4f, 0f, 0f),
            BackgroundColor = new Vector4D<float>(0.03f, 0.04f, 0.07f, 0.97f)
        };
        UiLayoutPresets.StretchAll(bodyOuter);
        var bodyInner = new UiPanel
        {
            Spacing = 0f,
            BackgroundColor = new Vector4D<float>(0.065f, 0.075f, 0.11f, 0.98f),
            Padding = new UiThickness(16f, 14f, 16f, 14f)
        };
        UiLayoutPresets.StretchAll(bodyInner);

        UiLayoutPresets.StretchAll(gatherPanel);
        gatherPanel.Visible = true;
        UiLayoutPresets.StretchAll(characterPanel);
        characterPanel.Visible = false;
        UiLayoutPresets.StretchAll(blacksmithPanel);
        blacksmithPanel.Visible = false;
        UiLayoutPresets.StretchAll(logPanel);
        logPanel.Visible = false;

        bodyInner.AddChild(gatherPanel);
        bodyInner.AddChild(characterPanel);
        bodyInner.AddChild(blacksmithPanel);
        bodyInner.AddChild(logPanel);
        bodyOuter.AddChild(bodyInner);

        rootStack.AddChild(chrome);
        rootStack.AddChild(bodyOuter);

        host.UiDocuments.Register(rootEntity, doc);

        navGroup.Select(NavGather);

        return new DocumentRefs
        {
            NavGroup = navGroup,
            GatherPanel = gatherPanel,
            CharacterPanel = characterPanel,
            BlacksmithPanel = blacksmithPanel,
            LogPanel = logPanel,
            ChromeGold = gold,
            ChromeGps = gps,
            SourceCards = sourceCards,
            StatRows = statRows,
            EquipCells = equipCells,
            LogScroll = logScroll,
            LogBody = logBody
        };
    }

    private static void SpawnFpsHud(World world, DocumentRefs refs)
    {
        var hudEntity = world.CreateEntity();
        world.GetOrAdd<FpsHudTag>(hudEntity);
        world.GetOrAdd<Transform>(hudEntity) = Transform.Identity;
        ref var text = ref world.GetOrAdd<BitmapText>(hudEntity);
        text.Visible = true;
        text.IsLocalizationKey = false;
        text.Content = "FPS —";
        text.Style = new TextStyle(BuiltinFonts.Mono, 14f, new Vector4D<float>(0.42f, 0.9f, 0.58f, 0.95f));
        text.SortKey = 870f;
        text.CoordinateSpace = CoordinateSpace.ViewportSpace;
        world.GetOrAdd<ViewportAnchor2D>(hudEntity) = new ViewportAnchor2D
        {
            Active = true,
            ContentSpace = CoordinateSpace.ViewportSpace,
            Anchor = ViewportAnchorPreset.TopRight,
            OffsetX = 96f,
            OffsetY = 18f,
            SyncSpriteHalfExtentsToViewport = false
        };

        refs.HasFpsHud = true;
        refs.FpsHudEntity = hudEntity;
    }

    private static void WireTabs(DocumentRefs refs)
    {
        refs.NavGroup.SelectionChanged += (_, id) =>
        {
            refs.GatherPanel.Visible = id == NavGather;
            refs.CharacterPanel.Visible = id == NavCharacter;
            refs.BlacksmithPanel.Visible = id == NavBlacksmith;
            refs.LogPanel.Visible = id == NavLog;
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

    private static UiRadioButton NavRadio(UiRadioGroup group, string id, string caption)
    {
        var rb = new UiRadioButton(group, id)
        {
            NormalTint = new Vector4D<float>(0.12f, 0.13f, 0.18f, 1f),
            SelectedTint = new Vector4D<float>(0.24f, 0.42f, 0.58f, 1f)
        };
        UiLayoutPresets.TopLeftFixed(rb, 158f, 38f);
        var lab = new UiLabel();
        UiLayoutPresets.StretchAll(lab);
        lab.Text.Text = caption;
        lab.Text.HorizontalAlignment = UiTextHorizontalAlignment.Center;
        lab.Text.VerticalAlignment = UiTextVerticalAlignment.Center;
        lab.Text.DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(0.93f, 0.95f, 1f, 1f), Bold: true);
        rb.AddChild(lab);
        rb.BackgroundColor = rb.NormalTint;
        return rb;
    }

    private static UiTextBlock TextBlock(string text, float size, Vector4D<float> color, bool bold = false) =>
        new()
        {
            Text = text,
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, size, color, Bold: bold)
        };

    private static (UiPanel panel, SourceCardRefs[] cards) BuildGatherTab(IRenderer renderer, LocalizationManager loc)
    {
        var panel = new UiPanel { Spacing = 8f };
        var intro = TextBlock(loc.Get("idlegold.ui.gather_intro"), 15f, new Vector4D<float>(0.75f, 0.8f, 0.88f, 1f));
        UiLayoutPresets.TopStretch(intro, 36f);

        var scroll = new UiScrollView();
        UiLayoutPresets.StretchAll(scroll);
        scroll.Content.Margin = new UiThickness(8f, 6f, 16f, 8f);
        var col = new UiVerticalStack { Spacing = 18f };
        UiLayoutPresets.StretchAll(col);

        ReadOnlySpan<Vector4D<float>> stripes =
        [
            new(0.35f, 0.55f, 0.85f, 1f),
            new(0.35f, 0.75f, 0.45f, 1f),
            new(0.75f, 0.4f, 0.85f, 1f),
            new(0.85f, 0.55f, 0.35f, 1f)
        ];

        var cards = new SourceCardRefs[4];
        for (var i = 0; i < 4; i++)
        {
            cards[i] = SourceCard(renderer, stripes[i]);
            col.AddChild(PanelFromRefs(cards[i]));
        }

        scroll.Content.AddChild(col);
        panel.AddChild(intro);
        panel.AddChild(scroll);
        return (panel, cards);
    }

    /// <summary>Wraps card controls in a layout panel (refs point at live widgets).</summary>
    private static UiPanel PanelFromRefs(SourceCardRefs c)
    {
        // Thin outer rim reads as a panel edge; inner card keeps the fill.
        var frame = new UiPanel
        {
            Spacing = 0f,
            BackgroundColor = new Vector4D<float>(0.28f, 0.42f, 0.58f, 0.42f),
            Padding = new UiThickness(1f)
        };
        UiLayoutPresets.StretchWidthAutoHeight(frame);

        var card = new UiPanel
        {
            Spacing = 8f,
            BackgroundColor = new Vector4D<float>(0.1f, 0.115f, 0.165f, 0.96f),
            Padding = new UiThickness(14f, 12f, 14f, 8f)
        };
        // Hug content height: StretchAll would fill the frame band and leave a tinted empty slab under the button.
        UiLayoutPresets.StretchWidthAutoHeight(card);

        // Band fits title + description; level/locked line lives beside the action button.
        const float headerBand = 52f;
        var header = new UiHorizontalStack { Spacing = 10f, CrossAlignment = UiCrossAlignment.Start };
        UiLayoutPresets.TopStretch(header, headerBand);
        UiLayoutPresets.TopLeftFixed(c.Stripe, 6f, headerBand);
        // Default (0,0) anchors + zero SizeDelta ignore the horizontal stack slot → 0×0 border box, 0-width text
        // measure, and broken hit geometry. TopStretch maps the slot width/height into ComputedBounds.
        var titles = new UiVerticalStack { Spacing = 5f };
        UiLayoutPresets.TopStretch(titles, headerBand);
        titles.AddChild(c.NameText);
        titles.AddChild(c.DescText);
        header.AddChild(c.Stripe);
        header.AddChild(titles);

        var actions = new UiHorizontalStack { Spacing = 12f, CrossAlignment = UiCrossAlignment.Center };
        UiLayoutPresets.TopStretch(actions, 44f);
        UiLayoutPresets.TopLeftFixed(c.DetailText, 222f, 40f);
        UiLayoutPresets.TopLeftFixed(c.UnlockButton, 154f, 40f);
        UiLayoutPresets.TopLeftFixed(c.LevelButton, 176f, 40f);
        actions.AddChild(c.UnlockButton);
        actions.AddChild(c.LevelButton);
        actions.AddChild(c.DetailText);

        card.AddChild(header);
        card.AddChild(actions);
        frame.AddChild(card);
        return frame;
    }

    private static SourceCardRefs SourceCard(IRenderer renderer, Vector4D<float> stripeTint)
    {
        var stripe = new UiImage { SourceTextureId = renderer.WhiteTextureId, Tint = stripeTint };
        var name = TextBlock(" ", 18f, new Vector4D<float>(0.98f, 0.99f, 1f, 1f), bold: true);
        UiLayoutPresets.TopStretch(name, 26f);
        var desc = TextBlock(" ", 13f, new Vector4D<float>(0.62f, 0.68f, 0.78f, 1f));
        UiLayoutPresets.TopStretch(desc, 22f);
        var detail = TextBlock(" ", 14f, new Vector4D<float>(0.52f, 0.88f, 0.96f, 1f));
        detail.HorizontalAlignment = UiTextHorizontalAlignment.End;
        detail.VerticalAlignment = UiTextVerticalAlignment.Center;

        var unlock = TextButton(154f, 40f, "unlock",
            new Vector4D<float>(0.24f, 0.36f, 0.5f, 1f),
            new Vector4D<float>(0.32f, 0.48f, 0.64f, 1f));
        var level = TextButton(176f, 40f, "level",
            new Vector4D<float>(0.14f, 0.44f, 0.54f, 1f),
            new Vector4D<float>(0.22f, 0.58f, 0.68f, 1f));

        return new SourceCardRefs
        {
            Stripe = stripe,
            NameText = name,
            DescText = desc,
            DetailText = detail,
            UnlockButton = unlock.Button,
            UnlockCaption = unlock.Caption,
            LevelButton = level.Button,
            LevelCaption = level.Caption
        };
    }

    private static (UiPanel panel, StatRowRefs[] rows) BuildCharacterTab(LocalizationManager loc)
    {
        var panel = new UiPanel { Spacing = 12f };
        var header = TextBlock(loc.Get("idlegold.ui.character_intro"), 15f, new Vector4D<float>(0.72f, 0.78f, 0.88f, 1f));
        UiLayoutPresets.TopStretch(header, 34f);

        var scroll = new UiScrollView();
        UiLayoutPresets.StretchAll(scroll);
        var col = new UiVerticalStack { Spacing = 12f };
        UiLayoutPresets.StretchAll(col);

        var rows = new StatRowRefs[4];
        for (var i = 0; i < 4; i++)
        {
            rows[i] = StatRow();
            col.AddChild(PanelFromStatRow(rows[i]));
        }

        scroll.Content.AddChild(col);
        panel.AddChild(header);
        panel.AddChild(scroll);
        return (panel, rows);
    }

    private static UiPanel PanelFromStatRow(StatRowRefs r)
    {
        var row = new UiPanel { Spacing = 8f, BackgroundColor = new Vector4D<float>(0.1f, 0.115f, 0.16f, 0.92f) };
        UiLayoutPresets.TopStretch(row, 92f);
        row.Padding = new UiThickness(12f, 10f, 12f, 10f);
        UiLayoutPresets.TopStretch(r.Summary, 28f);
        UiLayoutPresets.TopLeftFixed(r.BuyButton, 200f, 32f);
        row.AddChild(r.Summary);
        row.AddChild(r.BuyButton);
        return row;
    }

    private static StatRowRefs StatRow()
    {
        var summary = TextBlock(" ", 14f, new Vector4D<float>(0.82f, 0.86f, 0.92f, 1f));
        var buy = TextButton(200f, 34f, "buy",
            new Vector4D<float>(0.16f, 0.38f, 0.46f, 1f),
            new Vector4D<float>(0.22f, 0.52f, 0.6f, 1f));
        return new StatRowRefs { Summary = summary, BuyButton = buy.Button, BuyCaption = buy.Caption };
    }

    private static (UiPanel panel, EquipCellRefs[] cells) BuildBlacksmithTab(IRenderer renderer, LocalizationManager loc)
    {
        var panel = new UiPanel { Spacing = 12f };
        var header = TextBlock(loc.Get("idlegold.ui.blacksmith_intro"), 15f, new Vector4D<float>(0.72f, 0.78f, 0.88f, 1f));
        UiLayoutPresets.TopStretch(header, 34f);

        var grid = new UiGrid { ColumnCount = 3, Spacing = 10f };
        UiLayoutPresets.StretchAll(grid);

        var cells = new EquipCellRefs[5];
        for (var i = 0; i < 5; i++)
        {
            cells[i] = EquipCell(renderer);
            grid.AddChild(PanelFromEquipCell(cells[i]));
        }

        var spacer = new UiPanel { BackgroundColor = default, Visible = false };
        grid.AddChild(spacer);

        panel.AddChild(header);
        panel.AddChild(grid);
        return (panel, cells);
    }

    private static UiPanel PanelFromEquipCell(EquipCellRefs c)
    {
        var cell = new UiPanel
        {
            Spacing = 5f,
            BackgroundColor = new Vector4D<float>(0.1f, 0.115f, 0.155f, 0.93f),
            Padding = new UiThickness(10f, 10f, 10f, 10f)
        };
        UiLayoutPresets.CenterFixed(cell, 168f, 166f);
        UiLayoutPresets.TopLeftFixed(c.Icon, 36f, 36f);
        UiLayoutPresets.TopStretch(c.SlotText, 20f);
        UiLayoutPresets.TopStretch(c.TierText, 22f);
        UiLayoutPresets.TopLeftFixed(c.UpgradeButton, 148f, 32f);
        cell.AddChild(c.Icon);
        cell.AddChild(c.SlotText);
        cell.AddChild(c.TierText);
        cell.AddChild(c.UpgradeButton);
        return cell;
    }

    private static EquipCellRefs EquipCell(IRenderer renderer)
    {
        var icon = new UiImage { SourceTextureId = renderer.WhiteTextureId, Tint = TierVisual.Stripe(0) };
        var slot = TextBlock(" ", 13f, new Vector4D<float>(0.78f, 0.84f, 0.92f, 1f), bold: true);
        var tier = TextBlock(" ", 14f, new Vector4D<float>(0.9f, 0.92f, 1f, 1f), bold: true);
        var up = TextButton(148f, 34f, "up",
            new Vector4D<float>(0.16f, 0.38f, 0.46f, 1f),
            new Vector4D<float>(0.22f, 0.52f, 0.6f, 1f));
        return new EquipCellRefs
        {
            Icon = icon,
            SlotText = slot,
            TierText = tier,
            UpgradeButton = up.Button,
            UpgradeCaption = up.Caption
        };
    }

    private static (UiPanel panel, UiScrollView scroll, UiTextBlock body) BuildLogTab()
    {
        var panel = new UiPanel { Spacing = 8f };
        var scroll = new UiScrollView();
        UiLayoutPresets.StretchAll(scroll);
        var host = new UiPanel { Spacing = 0f };
        UiLayoutPresets.StretchAll(host);
        var body = TextBlock(" ", 13f, new Vector4D<float>(0.78f, 0.82f, 0.88f, 1f));
        UiLayoutPresets.StretchAll(body);
        host.AddChild(body);
        scroll.Content.AddChild(host);
        panel.AddChild(scroll);
        return (panel, scroll, body);
    }

    private sealed record TextButtonPair(UiButton Button, UiLabel Caption);

    private static TextButtonPair TextButton(float w, float h, string placeholder,
        Vector4D<float>? normalBg = null, Vector4D<float>? pressedBg = null)
    {
        var btn = new UiButton();
        if (normalBg.HasValue)
        {
            btn.NormalBackground = normalBg.Value;
            btn.BackgroundColor = normalBg.Value;
        }

        if (pressedBg.HasValue)
            btn.PressedBackground = pressedBg.Value;

        UiLayoutPresets.TopLeftFixed(btn, w, h);
        var lab = new UiLabel();
        UiLayoutPresets.StretchAll(lab);
        lab.Text.Text = placeholder;
        lab.Text.HorizontalAlignment = UiTextHorizontalAlignment.Center;
        lab.Text.VerticalAlignment = UiTextVerticalAlignment.Center;
        lab.Text.DefaultStyle = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(0.96f, 0.97f, 1f, 1f), Bold: true);
        btn.AddChild(lab);
        return new TextButtonPair(btn, lab);
    }

    private static void ApplyGlobalPost(World world)
    {
        var e = world.CreateEntity();
        world.GetOrAdd<GlobalPostProcessSource>(e) = new GlobalPostProcessSource
        {
            Active = true,
            Priority = 100,
            Settings = new GlobalPostProcessSettings
            {
                BloomEnabled = false,
                Exposure = 1f,
                Saturation = 1.05f,
                TonemapEnabled = true,
                EmissiveToHdrGain = 0.42f,
                EmissiveToBloomGain = 0.38f
            }
        };
    }
}
