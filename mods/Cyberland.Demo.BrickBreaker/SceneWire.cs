using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Tag-based wiring after <c>Scenes/brickbreaker.json</c> spawn.</summary>
internal static class SceneWire
{
    public static void Apply(World world)
    {
        var session = Session.RequireStateEntity(world);
        var cells = BuildCellGrid(world);

        world.GetOrAdd<ArenaBrickGrid>(session) = new ArenaBrickGrid { CellEntities = cells };
        world.GetOrAdd<ArenaLightRuntime>(session) = new ArenaLightRuntime
        {
            Paddle = world.RequireSingleEntityWith<Paddle>("BrickBreaker paddle"),
            Ball = world.RequireSingleEntityWith<BallTag>("BrickBreaker ball"),
            Ambient = world.RequireSingleEntityWith<AmbientLightTag>("BrickBreaker ambient light"),
            Directional = world.RequireSingleEntityWith<DirectionalLightTag>("BrickBreaker directional light"),
            Spot = world.RequireSingleEntityWith<ArenaSpotLightTag>("BrickBreaker arena spot"),
            PaddlePoint = world.RequireSingleEntityWith<PaddlePointLightTag>("BrickBreaker paddle point light"),
            BallPoint = world.RequireSingleEntityWith<BallPointLightTag>("BrickBreaker ball point light")
        };

        ref var panel = ref world.Get<Sprite>(world.RequireSingleEntityWith<GameOverPanelTag>("BrickBreaker game over panel"));
        panel.Alpha = 0.95f;
        panel.Transparent = true;
    }

    private static EntityId[] BuildCellGrid(World world)
    {
        var cells = new EntityId[Constants.Cols * Constants.Rows];
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<Cell>()))
        {
            var cellCol = chunk.Column<Cell>();
            var entities = chunk.Entities;
            for (var i = 0; i < entities.Length; i++)
            {
                var c = cellCol[i];
                cells[c.X + c.Y * Constants.Cols] = entities[i];
            }
        }

        return cells;
    }
}
