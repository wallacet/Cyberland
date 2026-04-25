using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Demo.Snake;

public struct Session : IComponent
{
    public Phase Phase;
    public float TickAcc;
    public int DirX;
    public int DirY;
    public int NextDirX;
    public int NextDirY;
    public LinkedList<(int x, int y)> Snake;
    public (int x, int y) Food;
    public int FoodsEaten;
    public float OriginX;
    public float OriginY;
    public float Cell;
    public void EnsureInitialized()
    {
        Snake ??= new LinkedList<(int x, int y)>();
    }
    /// <summary>
    /// Recomputes cell size and grid origin from framebuffer pixels. Safe to call from multiple systems each frame (idempotent inputs).
    /// </summary>
    public void UpdateLayout(float fbX, float fbY)
    {
        Cell = Math.Min(fbX / Constants.GridW, fbY / Constants.GridH);
        OriginX = (fbX - Constants.GridW * Cell) * 0.5f;
        OriginY = (fbY - Constants.GridH * Cell) * 0.5f;
    }
    public Vector2D<float> CellCenterScreen(int x, int y)
    {
        var rowFromTop = Constants.GridH - 1 - y;
        return new Vector2D<float>(OriginX + (x + 0.5f) * Cell, OriginY + (rowFromTop + 0.5f) * Cell);
    }
    public Vector2D<float> CellCenterWorld(int x, int y, Vector2D<int> framebufferSize) => WorldViewportSpace.ViewportPixelToWorldCenter(CellCenterScreen(x, y), framebufferSize);
    public void StartGame()
    {
        EnsureInitialized();
        Phase = Phase.Playing;
        FoodsEaten = 0;
        TickAcc = 0f;
        DirX = 1;
        DirY = 0;
        NextDirX = 1;
        NextDirY = 0;
        Snake.Clear();
        var wx = Constants.GridW / 2;
        var wy = (Constants.GridH - 1) / 2;
        for (var i = 0; i < 4; i++) Snake.AddLast((wx - i, wy));
        SpawnFood();
    }
    public void SpawnFood()
    {
        EnsureInitialized();
        for (var t = 0; t < 400; t++)
        {
            var fx = Random.Shared.Next(Constants.GridW);
            var fy = Random.Shared.Next(Constants.GridH);
            if (!IsOccupied(fx, fy))
            {
                Food = (fx, fy);
                return;
            }
        }
        Food = (0, 0);
    }
    private bool IsOccupied(int x, int y)
    {
        EnsureInitialized();
        foreach (var segment in Snake)
        {
            if (segment.x == x && segment.y == y)
                return true;
        }

        return false;
    }
    public void Step()
    {
        var head = Snake.First!.Value;
        var nx = head.x + DirX;
        var ny = head.y + DirY;
        if (nx < 0 || nx >= Constants.GridW || ny < 0 || ny >= Constants.GridH) { Phase = Phase.GameOver; return; }
        var willEat = nx == Food.x && ny == Food.y;
        var tail = Snake.Last!.Value;
        // Self-collision: skip the tail cell when we are not eating — the tail will move away this tick.
        foreach (var s in Snake)
        {
            if (!willEat && s.x == tail.x && s.y == tail.y) continue;
            if (s.x == nx && s.y == ny) { Phase = Phase.GameOver; return; }
        }
        Snake.AddFirst((nx, ny));
        if (willEat) { FoodsEaten++; SpawnFood(); } else Snake.RemoveLast();
    }
}
