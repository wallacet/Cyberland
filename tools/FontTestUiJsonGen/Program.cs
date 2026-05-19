// One-shot generator for mods/Cyberland.Demo.FontTest/Content/Ui/fonttest_matrix.json
using System.Text;
using System.Text.Json;

const string Sample =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123456789 !?.,:;+-=*/()[]{}<> \"'`~ @#$%^&|  \t  ";

var decorations = new (bool U, bool S, string Sfx)[]
{
    (false, false, "Plain"),
    (true, false, "Underline"),
    (false, true, "Strike"),
    (true, true, "Underline+Strike")
};

var builtinBases = new (string Family, float Size, bool Bold, bool Italic, string Label)[]
{
    ("UiSans", 12f, false, false, "UiSans 12 Regular"),
    ("UiSans", 13f, false, false, "UiSans 13 Regular"),
    ("UiSans", 14f, false, false, "UiSans 14 Regular"),
    ("UiSans", 15f, false, false, "UiSans 15 Regular"),
    ("UiSans", 16f, false, false, "UiSans 16 Regular"),
    ("UiSans", 18f, false, false, "UiSans 18 Regular"),
    ("UiSans", 20f, false, false, "UiSans 20 Regular"),
    ("UiSans", 22f, false, false, "UiSans 22 Regular"),
    ("UiSans", 23f, false, false, "UiSans 23 Regular"),
    ("UiSans", 24f, false, false, "UiSans 24 Regular"),
    ("UiSans", 14f, true, false, "UiSans 14 Bold"),
    ("UiSans", 18f, true, false, "UiSans 18 Bold"),
    ("UiSans", 23f, true, false, "UiSans 23 Bold"),
    ("Mono", 14f, false, false, "Mono 14 Regular"),
    ("Mono", 18f, false, false, "Mono 18 Regular")
};

var jostBases = new (string Family, float Size, bool Bold, bool Italic, string Label)[]
{
    ("Jost", 12f, false, false, "Jost 12 Regular"),
    ("Jost", 13f, false, false, "Jost 13 Regular"),
    ("Jost", 14f, false, false, "Jost 14 Regular"),
    ("Jost", 15f, false, false, "Jost 15 Regular"),
    ("Jost", 16f, false, false, "Jost 16 Regular"),
    ("Jost", 17f, false, false, "Jost 17 Regular"),
    ("Jost", 18f, false, false, "Jost 18 Regular"),
    ("Jost", 20f, false, false, "Jost 20 Regular"),
    ("Jost", 22f, false, false, "Jost 22 Regular"),
    ("Jost", 23f, false, false, "Jost 23 Regular"),
    ("Jost", 24f, false, false, "Jost 24 Regular"),
    ("Jost", 14f, true, false, "Jost 14 Bold"),
    ("Jost", 18f, true, false, "Jost 18 Bold"),
    ("Jost", 23f, true, false, "Jost 23 Bold")
};

var rows = new List<object>();
rows.Add(Title("Built-in font matrix (UiSans / Mono) — mouse wheel to scroll. One row per size/style/decoration."));
rows.AddRange(Expand("UiSans", new[] { 0.9f, 0.95f, 1f, 1f }, builtinBases));
rows.Add(Title("Custom path: Jost (SIL OFL) — mod Content TTF + RegisterFamilyFromVirtualPathsAsync; MSDF atlases in Content/Fonts/Baked."));
rows.AddRange(Expand("Jost", new[] { 0.98f, 0.82f, 0.52f, 1f }, jostBases));

var root = new Dictionary<string, object?>
{
    ["schemaVersion"] = 1,
    ["root"] = new Dictionary<string, object?>
    {
        ["type"] = "cyberland.engine/panel",
        ["backgroundColor"] = Vec4(0.04f, 0.06f, 0.12f, 1f),
        ["layout"] = new Dictionary<string, object?> { ["preset"] = "stretchAll" },
        ["children"] = new[]
        {
            new Dictionary<string, object?>
            {
                ["type"] = "cyberland.engine/scroll-view",
                ["wheelScrollPixels"] = 44f,
                ["layout"] = new Dictionary<string, object?> { ["preset"] = "stretchAll" },
                ["content"] = new Dictionary<string, object?>
                {
                    ["type"] = "cyberland.engine/vertical-stack",
                    ["spacing"] = 10f,
                    ["layout"] = new Dictionary<string, object?> { ["preset"] = "stretchAll" },
                    ["margin"] = new Dictionary<string, float> { ["left"] = 18f, ["top"] = 16f, ["right"] = 22f, ["bottom"] = 20f },
                    ["children"] = rows
                }
            }
        }
    }
};

var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
var outPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "mods", "Cyberland.Demo.FontTest", "Content", "Ui", "fonttest_matrix.json"));
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
await File.WriteAllTextAsync(outPath, json, Encoding.UTF8);
Console.WriteLine($"Wrote {outPath}");

static object Title(string text) => new Dictionary<string, object?>
{
    ["type"] = "cyberland.engine/text-block",
    ["text"] = text,
    ["fontFamily"] = CanonFamily("UiSans"),
    ["sizePixels"] = 15f,
    ["bold"] = true,
    ["color"] = Vec4(0.88f, 0.93f, 1f, 1f),
    ["verticalAlignment"] = "CenterInk",
    ["layout"] = new Dictionary<string, object?> { ["preset"] = "topStretch", ["height"] = 52f }
};

static IEnumerable<object> Expand(string family, float[] color, (string Family, float Size, bool Bold, bool Italic, string Label)[] bases)
{
    var decs = new (bool U, bool S, string Sfx)[]
    {
        (false, false, "Plain"),
        (true, false, "Underline"),
        (false, true, "Strike"),
        (true, true, "Underline+Strike")
    };
    foreach (var b in bases)
    {
        foreach (var d in decs)
        {
            var label = $"{b.Label} {d.Sfx}";
            var h = RowHeight(b.Size, d.U, d.S);
            yield return new Dictionary<string, object?>
            {
                ["type"] = "cyberland.engine/text-block",
                ["text"] = $"{label}: {Sample}",
                ["fontFamily"] = CanonFamily(family),
                ["sizePixels"] = b.Size,
                ["bold"] = b.Bold,
                ["italic"] = b.Italic,
                ["underline"] = d.U,
                ["strikethrough"] = d.S,
                ["color"] = Vec4(color[0], color[1], color[2], color[3]),
                ["verticalAlignment"] = "Start",
                ["layout"] = new Dictionary<string, object?> { ["preset"] = "topStretch", ["height"] = h }
            };
        }
    }
}

static float RowHeight(float size, bool underline, bool strike)
{
    var h = size * 1.55f + 10f;
    if (underline) h += 10f;
    if (strike) h += 8f;
    return MathF.Ceiling(MathF.Max(36f, h));
}

static string CanonFamily(string family) =>
    family switch
    {
        "UiSans" => "cyberland.engine/ui",
        "Mono" => "cyberland.engine/mono",
        "Jost" => "fonttest.jost",
        _ => family
    };

static Dictionary<string, float> Vec4(float x, float y, float z, float w) =>
    new() { ["x"] = x, ["y"] = y, ["z"] = z, ["w"] = w };
