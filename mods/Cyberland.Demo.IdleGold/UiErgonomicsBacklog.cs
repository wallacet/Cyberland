namespace Cyberland.Demo.IdleGold;

/// <summary>
/// Scratchpad for UI friction noticed while building this demo — triage entries into Engine issues vs mod-local helpers.
/// </summary>
/// <remarks>
/// <para><b>Scroll:</b> log stick-to-bottom uses a large <see cref="Cyberland.Engine.UI.Controls.UiScrollView.VerticalOffset"/> value; a first-class <c>ScrollToEnd()</c> (and clamp) would be clearer.</para>
/// <para><b>Commands:</b> <see cref="Cyberland.Engine.Hosting.GameHostServices.UiCommands"/> is untyped; generic enqueue/dispatch would avoid scattered casts.</para>
/// <para><b>Buttons:</b> disabled affordance is only <see cref="Cyberland.Engine.UI.Core.UiElement.Interactable"/> — no built-in muted palette or hover state.</para>
/// <para><b>Composition:</b> labeled buttons need repeating panel+label wiring; a small factory or <c>UiCommandButton</c> would cut boilerplate.</para>
/// <para><b>Localization:</b> formatted strings use <c>string.Format</c> manually; binding keys + args on text widgets could be Engine-owned.</para>
/// </remarks>
public static class UiErgonomicsBacklog;
