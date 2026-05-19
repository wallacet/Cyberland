using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Layout;
using Cyberland.Engine.UI.Text;

namespace Cyberland.Demo.IdleGold;

/// <summary>Strong handles for retained UI loaded from <c>Content/Ui/idlegold_hud.json</c>.</summary>
public sealed class DocumentRefs
{
    public UiRadioGroup NavGroup { get; init; } = null!;

    public UiPanel GatherPanel { get; init; } = null!;
    public UiPanel CharacterPanel { get; init; } = null!;
    public UiPanel BlacksmithPanel { get; init; } = null!;
    public UiPanel LogPanel { get; init; } = null!;

    public UiTextBlock ChromeGold { get; init; } = null!;
    public UiTextBlock ChromeGps { get; init; } = null!;

    public SourceCardRefs[] SourceCards { get; init; } = null!;

    public StatRowRefs[] StatRows { get; init; } = null!;

    public EquipCellRefs[] EquipCells { get; init; } = null!;

    public UiScrollView LogScroll { get; init; } = null!;
    public UiTextBlock LogBody { get; init; } = null!;
    public string CurrentTabId { get; set; } = Mod.NavGather;

    public bool HasFpsHud { get; set; }

    /// <summary>Viewport <see cref="BitmapText"/> FPS overlay when <see cref="HasFpsHud"/>.</summary>
    public EntityId FpsHudEntity { get; set; }
}

public sealed class SourceCardRefs
{
    public UiTextBlock NameText { get; init; } = null!;
    public UiTextBlock DescText { get; init; } = null!;
    public UiTextBlock DetailText { get; init; } = null!;
    public UiButton UnlockButton { get; init; } = null!;
    public UiLabel UnlockCaption { get; init; } = null!;
    public UiButton LevelButton { get; init; } = null!;
    public UiLabel LevelCaption { get; init; } = null!;
    public UiImage Stripe { get; init; } = null!;
}

public sealed class StatRowRefs
{
    public UiTextBlock Summary { get; init; } = null!;
    public UiButton BuyButton { get; init; } = null!;
    public UiLabel BuyCaption { get; init; } = null!;
}

public sealed class EquipCellRefs
{
    public UiImage Icon { get; init; } = null!;
    public UiTextBlock SlotText { get; init; } = null!;
    public UiTextBlock TierText { get; init; } = null!;
    public UiButton UpgradeButton { get; init; } = null!;
    public UiLabel UpgradeCaption { get; init; } = null!;
}
