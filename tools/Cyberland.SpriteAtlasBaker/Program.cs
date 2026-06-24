using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: Cyberland.SpriteAtlasBaker <inputFolder> <outputManifest.json> [pageSize]");
    return 1;
}

var inputFolder = Path.GetFullPath(args[0]);
var outputManifest = Path.GetFullPath(args[1]);
var pageSize = args.Length >= 3 && int.TryParse(args[2], out var ps) ? ps : 2048;

if (!Directory.Exists(inputFolder))
{
    Console.Error.WriteLine($"Input folder not found: {inputFolder}");
    return 1;
}

var pngs = Directory.GetFiles(inputFolder, "*.png", SearchOption.TopDirectoryOnly)
    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
    .ToArray();
if (pngs.Length == 0)
{
    Console.Error.WriteLine("No PNG files found in input folder.");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(outputManifest)!);
var manifestDir = Path.GetDirectoryName(outputManifest)!;
var atlasBaseName = Path.GetFileNameWithoutExtension(outputManifest);
var pagePath = Path.Combine(manifestDir, atlasBaseName + ".page0.png");

using var page = new Image<Rgba32>(pageSize, pageSize);
page.Mutate(ctx => ctx.BackgroundColor(new Rgba32(0, 0, 0, 0)));

var regions = new List<object>();
var x = 0;
var y = 0;
var rowHeight = 0;

foreach (var png in pngs)
{
    using var src = Image.Load<Rgba32>(png);
    if (x + src.Width > pageSize)
    {
        x = 0;
        y += rowHeight;
        rowHeight = 0;
    }

    if (y + src.Height > pageSize)
    {
        Console.Error.WriteLine($"Atlas page overflow at {Path.GetFileName(png)}; increase page size or split atlases.");
        return 1;
    }

    page.Mutate(ctx => ctx.DrawImage(src, new Point(x, y), 1f));
    var name = Path.GetFileNameWithoutExtension(png);
    regions.Add(new
    {
        name,
        pageIndex = 0,
        pixelRect = new[] { x, y, src.Width, src.Height },
        pivot = new[] { 0.5f, 0.5f },
        sizeWorld = new[] { (float)src.Width, (float)src.Height },
        nineSlice = (int[]?)null
    });

    x += src.Width;
    rowHeight = Math.Max(rowHeight, src.Height);
}

page.SaveAsPng(pagePath);

var manifestRelativePage = Path.GetRelativePath(manifestDir, pagePath).Replace('\\', '/');
var manifest = new
{
    schemaVersion = 1,
    pages = new[] { new { path = manifestRelativePage } },
    regions,
    animations = new { },
    sheets = new { }
};

await File.WriteAllTextAsync(outputManifest, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Wrote {outputManifest} with {regions.Count} regions and page {pagePath}");
return 0;
