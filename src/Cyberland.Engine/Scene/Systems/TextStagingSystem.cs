using System.Threading;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Validates and prepares <see cref="BitmapText"/> rows before <see cref="TextRenderSystem"/> draws them.
/// </summary>
/// <remarks>
/// Reports a diagnostic once per problematic configuration: <see cref="BitmapText.Visible"/> with null/empty
/// <see cref="BitmapText.Content"/> (localization resolution happens later in <see cref="TextRenderSystem"/>).
/// </remarks>
public sealed class TextStagingSystem : ISystem, ILateUpdate
{
    private static int _warnedVisibleEmpty;

    private readonly GameHostServices _host;
    private bool _haveColumnMap;
    private int _colBitmapText;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<BitmapText, Position>();

    /// <summary>Creates the system.</summary>
    public TextStagingSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = _host.Renderer
            ?? throw new InvalidOperationException("TextStagingSystem requires Host.Renderer during OnStart.");
        EnsureColumnMap(world, archetype.Spec);
    }

    /// <inheritdoc />
    public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = world;
        _ = deltaSeconds;
        if (!_haveColumnMap)
            EnsureColumnMap(world, archetype.Spec);

        foreach (var chunk in archetype)
        {
            var texts = chunk.Column<BitmapText>(_colBitmapText);
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

    private void EnsureColumnMap(World world, SystemQuerySpec spec)
    {
        _colBitmapText = world.GetQueryColumnIndex<BitmapText>(spec);
        _haveColumnMap = true;
    }
}
