using System.Threading;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Validates and prepares <see cref="BitmapText"/> rows before <see cref="TextBuildSystem"/> and <see cref="TextRenderSystem"/>.
/// </summary>
/// <remarks>
/// Reports a diagnostic once per problematic configuration: <see cref="BitmapText.Visible"/> with null/empty
/// <see cref="BitmapText.Content"/> (localization resolution happens in <see cref="TextBuildSystem"/> / <see cref="TextRenderSystem"/>).
/// </remarks>
public sealed class TextStagingSystem : IParallelSystem, IParallelLateUpdate, ILateUpdate
{
    private static int _warnedVisibleEmpty;
    private static readonly ParallelOptions SequentialCompatParallelOptions = new() { MaxDegreeOfParallelism = 1 };

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<BitmapText, Transform>();

    /// <summary>Creates the system.</summary>
    public TextStagingSystem(GameHostServices host) => _ = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = world;
        _ = archetype;
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds) =>
        OnParallelLateUpdate(archetype, deltaSeconds, SequentialCompatParallelOptions);

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
