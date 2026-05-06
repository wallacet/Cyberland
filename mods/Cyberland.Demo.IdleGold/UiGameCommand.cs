namespace Cyberland.Demo.IdleGold;

/// <summary>Player intents queued from <see cref="Cyberland.Engine.UI.Controls.UiButton"/> and drained with <see cref="Cyberland.Engine.Hosting.GameHostServices.UiCommandDispatcher"/>.</summary>
public abstract record UiGameCommand;

public sealed record UnlockSourceCommand(SourceId Source) : UiGameCommand;

public sealed record LevelSourceCommand(SourceId Source) : UiGameCommand;

public sealed record BuyStatCommand(StatKind Stat) : UiGameCommand;

public sealed record UpgradeEquipmentCommand(EquipSlot Slot) : UiGameCommand;
