using System.Text.Json;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.RuntimeScenes;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Serialization;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Layout;
using Cyberland.Engine.UI.Text;
using Silk.NET.Maths;

namespace Cyberland.Engine.RuntimeUi;

/// <summary>Registers built-in <c>cyberland.engine/*</c> UI JSON element deserializers.</summary>
public static class EngineUiElementDeserializers
{
    /// <summary>Registers all stock UI element types on <paramref name="ui"/>.</summary>
    public static void Register(IUiRuntime ui, IRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(renderer);

        ui.RegisterElementDeserializer("cyberland.engine/panel", static (in UiElementDeserializeContext ctx) => new UiPanel());
        ui.RegisterElementDeserializer("cyberland.engine/vertical-stack", static (in UiElementDeserializeContext ctx) => new UiVerticalStack());
        ui.RegisterElementDeserializer("cyberland.engine/horizontal-stack", static (in UiElementDeserializeContext ctx) => new UiHorizontalStack());
        ui.RegisterElementDeserializer("cyberland.engine/grid", static (in UiElementDeserializeContext ctx) => new UiGrid());
        ui.RegisterElementDeserializer("cyberland.engine/scroll-view", static (in UiElementDeserializeContext ctx) => new UiScrollView());
        ui.RegisterElementDeserializer("cyberland.engine/image", static (in UiElementDeserializeContext ctx) => new UiImage());
        ui.RegisterElementDeserializer("cyberland.engine/text-block", static (in UiElementDeserializeContext ctx) => new UiTextBlock());
        ui.RegisterElementDeserializer("cyberland.engine/label", static (in UiElementDeserializeContext ctx) => new UiLabel());
        ui.RegisterElementDeserializer("cyberland.engine/button", static (in UiElementDeserializeContext ctx) => new UiButton());
        ui.RegisterElementDeserializer("cyberland.engine/radio-button", static (in UiElementDeserializeContext ctx) =>
        {
            var groupId = RuntimeJsonReaders.ReadString(ctx.Node, "groupId")
                ?? throw new InvalidOperationException("radio-button requires groupId.");
            var optionId = RuntimeJsonReaders.ReadString(ctx.Node, "optionId")
                ?? throw new InvalidOperationException("radio-button requires optionId.");
            var w = RuntimeJsonReaders.ReadFloat(ctx.Node, "width", 48f);
            var h = RuntimeJsonReaders.ReadFloat(ctx.Node, "height", 28f);
            var group = ctx.Session.GetOrCreateRadioGroup(groupId);
            return new UiRadioButton(group, optionId, w, h);
        });

        _ = renderer;
    }

    /// <summary>Applies shared JSON fields and registers <c>id</c> on <paramref name="element"/>.</summary>
    public static void ApplySharedElementFields(UiElement element, JsonElement node, UiBuildSession session)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.Visible = RuntimeJsonReaders.ReadBool(node, "visible", true);
        element.Interactable = RuntimeJsonReaders.ReadBool(node, "interactable", element.Interactable);
        element.SortKey = RuntimeJsonReaders.ReadFloat(node, "sortKey", element.SortKey);
        element.ClipMode = RuntimeJsonReaders.ReadEnum(node, "clipMode", element.ClipMode);
        element.Margin = RuntimeJsonReaders.ReadThickness(node, "margin", element.Margin);
        element.Padding = RuntimeJsonReaders.ReadThickness(node, "padding", element.Padding);

        if (RuntimeJsonReaders.TryReadVec2(node, "anchorMin", out var amin))
            element.AnchorMin = amin;
        if (RuntimeJsonReaders.TryReadVec2(node, "anchorMax", out var amax))
            element.AnchorMax = amax;
        if (RuntimeJsonReaders.TryReadVec2(node, "pivot", out var pivot))
            element.Pivot = pivot;
        if (RuntimeJsonReaders.TryReadVec2(node, "anchoredPosition", out var ap))
            element.AnchoredPosition = ap;
        if (RuntimeJsonReaders.TryReadVec2(node, "sizeDelta", out var sd))
            element.SizeDelta = sd;
        if (node.TryGetProperty("stretch", out var stretch) && stretch.ValueKind == JsonValueKind.Object)
        {
            element.StretchLeft = RuntimeJsonReaders.ReadFloat(stretch, "left", element.StretchLeft);
            element.StretchRight = RuntimeJsonReaders.ReadFloat(stretch, "right", element.StretchRight);
            element.StretchTop = RuntimeJsonReaders.ReadFloat(stretch, "top", element.StretchTop);
            element.StretchBottom = RuntimeJsonReaders.ReadFloat(stretch, "bottom", element.StretchBottom);
        }

        ApplyLayoutPreset(element, node);

        var id = RuntimeJsonReaders.ReadString(node, "id");
        if (!string.IsNullOrWhiteSpace(id))
            session.RegisterElementId(id, element);
    }

    /// <summary>Applies type-specific fields after shared fields.</summary>
    public static void ApplyTypedFields(UiElement element, JsonElement node, IRenderer renderer)
    {
        switch (element)
        {
            case UiHorizontalStack hstack:
                hstack.Spacing = RuntimeJsonReaders.ReadFloat(node, "spacing", hstack.Spacing);
                hstack.CrossAlignment = RuntimeJsonReaders.ReadEnum(node, "crossAlignment", hstack.CrossAlignment);
                break;
            case UiGrid grid:
                grid.ColumnCount = RuntimeJsonReaders.ReadInt(node, "columnCount", grid.ColumnCount);
                grid.Spacing = RuntimeJsonReaders.ReadFloat(node, "spacing", grid.Spacing);
                break;
            case UiScrollView scroll:
                scroll.WheelScrollPixels = RuntimeJsonReaders.ReadFloat(node, "wheelScrollPixels", scroll.WheelScrollPixels);
                break;
            case UiImage image:
                image.SourceTextureId = ReadBuiltinTexture(node, "sourceTexture", renderer, image.SourceTextureId);
                if (RuntimeJsonReaders.TryReadVec4(node, "tint", out var tint))
                    image.Tint = tint;
                break;
            case UiTextBlock text:
                ApplyTextBlockFields(text, node);
                break;
            case UiButton button:
                if (RuntimeJsonReaders.TryReadVec4(node, "normalBackground", out var nb))
                    button.NormalBackground = nb;
                if (RuntimeJsonReaders.TryReadVec4(node, "pressedBackground", out var pb))
                    button.PressedBackground = pb;
                button.BackgroundColor = button.NormalBackground;
                break;
            case UiLabel label:
                if (RuntimeJsonReaders.TryReadVec4(node, "backgroundColor", out var lbg))
                    label.BackgroundColor = lbg;
                label.Spacing = RuntimeJsonReaders.ReadFloat(node, "spacing", label.Spacing);
                ApplyTextBlockFields(label.Text, node);
                break;
            case UiRadioButton radio:
                if (RuntimeJsonReaders.TryReadVec4(node, "normalTint", out var nt))
                    radio.NormalTint = nt;
                if (RuntimeJsonReaders.TryReadVec4(node, "selectedTint", out var st))
                    radio.SelectedTint = st;
                radio.SyncVisual();
                break;
            case UiPanel panel:
                panel.Spacing = RuntimeJsonReaders.ReadFloat(node, "spacing", panel.Spacing);
                if (RuntimeJsonReaders.TryReadVec4(node, "backgroundColor", out var bg))
                    panel.BackgroundColor = bg;
                panel.BackgroundTextureId = ReadBuiltinTexture(node, "backgroundTexture", renderer, panel.BackgroundTextureId);
                break;
        }
    }

    /// <summary>Builds children under <paramref name="parent"/> from a <c>children</c> array.</summary>
    public static void BuildChildren(
        UiElement parent,
        JsonElement node,
        UiDocument document,
        UiBuildSession session,
        IRenderer renderer,
        ILocalizedContentStrings? strings,
        IReadOnlyDictionary<string, UiElementDeserializer> deserializers,
        bool allowUnknownElementTypes)
    {
        if (!node.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
            return;

        foreach (var childNode in children.EnumerateArray())
            UiRuntime.BuildAndAttachChild(parent, childNode, document, session, renderer, strings, deserializers, allowUnknownElementTypes);
    }

    /// <summary>Deserializes scroll-view <c>content</c> subtree into <see cref="UiScrollView.Content"/>.</summary>
    public static void BuildScrollViewContent(
        UiScrollView scroll,
        JsonElement node,
        UiDocument document,
        UiBuildSession session,
        IRenderer renderer,
        ILocalizedContentStrings? strings,
        IReadOnlyDictionary<string, UiElementDeserializer> deserializers,
        bool allowUnknownElementTypes)
    {
        if (!node.TryGetProperty("content", out var contentNode) || contentNode.ValueKind != JsonValueKind.Object)
            return;

        while (scroll.Content.Children.Count > 0)
            scroll.Content.RemoveChild(scroll.Content.Children[0]);

        var built = UiRuntime.BuildElement(contentNode, document, session, renderer, strings, deserializers, allowUnknownElementTypes);
        scroll.Content.AddChild(built);
    }

    private static void ApplyTextBlockFields(UiTextBlock text, JsonElement node)
    {
        var locKey = RuntimeJsonReaders.ReadString(node, "locKey");
        var literal = RuntimeJsonReaders.ReadString(node, "text");
        var isLocalizationKey = RuntimeJsonReaders.ReadBool(node, "isLocalizationKey", false);

        var family = RuntimeJsonReaders.ResolveFontFamilyId(RuntimeJsonReaders.ReadString(node, "fontFamily"));
        var size = RuntimeJsonReaders.ReadFloat(node, "sizePixels", 16f);
        var color = RuntimeJsonReaders.TryReadVec4(node, "color", out var c)
            ? c
            : new Vector4D<float>(1f, 1f, 1f, 1f);
        text.DefaultStyle = new TextStyle(
            family,
            size,
            color,
            RuntimeJsonReaders.ReadBool(node, "bold", false),
            RuntimeJsonReaders.ReadBool(node, "italic", false),
            RuntimeJsonReaders.ReadBool(node, "underline", false),
            RuntimeJsonReaders.ReadBool(node, "strikethrough", false));

        if (isLocalizationKey)
        {
            var key = !string.IsNullOrEmpty(locKey) ? locKey : literal ?? "";
            if (key.Length > 0)
            {
                text.Text = key;
                text.Runs = [new TextRun(key, text.DefaultStyle, isLocalizationKey: true)];
            }
        }
        else if (!string.IsNullOrEmpty(locKey))
            text.Text = locKey;
        else if (literal is not null)
            text.Text = literal;

        text.VerticalAlignment = RuntimeJsonReaders.ReadEnum(node, "verticalAlignment", text.VerticalAlignment);
        text.HorizontalAlignment = RuntimeJsonReaders.ReadEnum(node, "horizontalAlignment", text.HorizontalAlignment);
        text.ParagraphSpacing = RuntimeJsonReaders.ReadFloat(node, "paragraphSpacing", text.ParagraphSpacing);
        text.LineSpacingExtra = RuntimeJsonReaders.ReadFloat(node, "lineSpacingExtra", text.LineSpacingExtra);
        text.FitMode = RuntimeJsonReaders.ReadEnum(node, "fitMode", text.FitMode);
        text.FitTarget = RuntimeJsonReaders.ReadEnum(node, "fitTarget", text.FitTarget);
        text.MinFitSizePixels = RuntimeJsonReaders.ReadFloat(node, "minFitSizePixels", text.MinFitSizePixels);
    }

    private static TextureId ReadBuiltinTexture(JsonElement node, string name, IRenderer renderer, TextureId fallback)
    {
        var s = RuntimeJsonReaders.ReadString(node, name);
        if (string.IsNullOrWhiteSpace(s))
            return fallback;
        return s.Equals("white", StringComparison.OrdinalIgnoreCase)
            ? renderer.WhiteTextureId
            : s.Equals("defaultNormal", StringComparison.OrdinalIgnoreCase)
                ? renderer.DefaultNormalTextureId
                : fallback;
    }

    private static void ApplyLayoutPreset(UiElement element, JsonElement node)
    {
        if (!node.TryGetProperty("layout", out var layout) || layout.ValueKind != JsonValueKind.Object)
            return;

        var preset = RuntimeJsonReaders.ReadString(layout, "preset");
        if (string.IsNullOrWhiteSpace(preset))
            return;

        switch (preset.ToLowerInvariant())
        {
            case "stretchall":
                UiLayoutPresets.StretchAll(element);
                break;
            case "stretchwidthautoheight":
                UiLayoutPresets.StretchWidthAutoHeight(element);
                break;
            case "topstretch":
                UiLayoutPresets.TopStretch(element, RuntimeJsonReaders.ReadFloat(layout, "height", 32f));
                break;
            case "topleftfixed":
                UiLayoutPresets.TopLeftFixed(element,
                    RuntimeJsonReaders.ReadFloat(layout, "width", 64f),
                    RuntimeJsonReaders.ReadFloat(layout, "height", 32f));
                break;
            case "centerfixed":
                UiLayoutPresets.CenterFixed(element,
                    RuntimeJsonReaders.ReadFloat(layout, "width", 64f),
                    RuntimeJsonReaders.ReadFloat(layout, "height", 32f));
                break;
            case "toprightfixed":
                UiLayoutPresets.TopRightFixed(element,
                    RuntimeJsonReaders.ReadFloat(layout, "width", 64f),
                    RuntimeJsonReaders.ReadFloat(layout, "height", 32f),
                    RuntimeJsonReaders.ReadFloat(layout, "margin", 0f));
                break;
            case "bottomrightfixed":
                UiLayoutPresets.BottomRightFixed(element,
                    RuntimeJsonReaders.ReadFloat(layout, "width", 64f),
                    RuntimeJsonReaders.ReadFloat(layout, "height", 32f),
                    RuntimeJsonReaders.ReadFloat(layout, "margin", 0f));
                break;
        }

        if (RuntimeJsonReaders.TryReadVec2(layout, "anchoredPosition", out var ap))
            element.AnchoredPosition = ap;
    }
}
