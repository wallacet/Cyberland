using System.Threading;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Cheap validation pass for <see cref="BitmapText"/> rows (visible + empty content). Does not touch glyph caches.
/// </summary>
/// <remarks>
/// Runs as <see cref="IParallelLateUpdate"/> in parallel with other late systems; it only walks BitmapText/Transform and
/// may emit a one-time diagnostic. All glyph layout happens in <see cref="TextRenderSystem"/>.
/// </remarks>
public sealed class TextStagingSystem : IParallelSystem, IParallelLateUpdate
{
    private static int _warnedVisibleEmpty;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<BitmapText, Transform>();

    /// <summary>Creates the system.</summary>
    public TextStagingSystem() { }

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = world;
        _ = archetype;
    }

    /// <inheritdoc />
    public void OnParallelLateUpdate(ChunkQueryAll archetype, float deltaSeconds, ParallelOptions options)
    {
        _ = deltaSeconds;
        _ = options;

        foreach (var chunk in archetype)
        {
            var texts = chunk.Column<BitmapText>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var bt = ref texts[i];
                if (!bt.Visible)
                    continue;
                if (!string.IsNullOrEmpty(bt.Content))
                    continue;
                if (Interlocked.CompareExchange(ref _warnedVisibleEmpty, 1, 0) == 0)
                {
                    EngineDiagnostics.Report(EngineErrorSeverity.Warning, "Cyberland.Engine.TextStagingSystem",
                        "At least one BitmapText row has Visible=true with empty Content; those rows will not draw.");
                }
            }
        }
    }
}
