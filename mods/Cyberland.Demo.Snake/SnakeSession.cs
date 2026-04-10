namespace Cyberland.Demo.Snake;

/// <summary>
/// Snake body and food live in heap collections (not ECS chunks). Systems read/write this session from split <see cref="ISystem"/> passes.
/// </summary>
public sealed class SnakeSession
{
    public SnakePhase Phase = SnakePhase.Title;
    public float TickAcc;
    public int DirX = 1;
    public int DirY;
    public int NextDirX = 1;
    public int NextDirY;
    public readonly LinkedList<(int x, int y)> Snake = new();
    public (int x, int y) Food;
    public int FoodsEaten;
    public float OriginX;
    public float OriginY;
    public float Cell;

    /// <summary>Recompute grid fit from framebuffer pixels (call each frame before sim/render).</summary>
    public void UpdateLayout(float fbX, float fbY)
    {
        Cell = Math.Min(fbX / SnakeConstants.GridW, fbY / SnakeConstants.GridH);
        OriginX = (fbX - SnakeConstants.GridW * Cell) * 0.5f;
        OriginY = (fbY - SnakeConstants.GridH * Cell) * 0.5f;
    }

    public void StartGame()
    {
        Phase = SnakePhase.Playing;
        FoodsEaten = 0;
        TickAcc = 0f;
        DirX = 1;
        DirY = 0;
        NextDirX = 1;
        NextDirY = 0;
        Snake.Clear();
        var cx = SnakeConstants.GridW / 2;
        var cy = SnakeConstants.GridH / 2;
        for (var i = 0; i < 4; i++)
            Snake.AddLast((cx - i, cy));
        SpawnFood();
    }

    public void SpawnFood()
    {
        var occupied = new HashSet<(int, int)>(Snake);
        for (var t = 0; t < 400; t++)
        {
            var fx = Random.Shared.Next(SnakeConstants.GridW);
            var fy = Random.Shared.Next(SnakeConstants.GridH);
            if (occupied.Add((fx, fy)))
            {
                Food = (fx, fy);
                return;
            }
        }

        Food = (0, 0);
    }

    public void Step()
    {
        var head = Snake.First!.Value;
        var nx = head.x + DirX;
        var ny = head.y + DirY;
        if (nx < 0 || nx >= SnakeConstants.GridW || ny < 0 || ny >= SnakeConstants.GridH)
        {
            Phase = SnakePhase.GameOver;
            return;
        }

        var willEat = nx == Food.x && ny == Food.y;
        var tail = Snake.Last!.Value;
        foreach (var s in Snake)
        {
            if (!willEat && s.x == tail.x && s.y == tail.y)
                continue;
            if (s.x == nx && s.y == ny)
            {
                Phase = SnakePhase.GameOver;
                return;
            }
        }

        Snake.AddFirst((nx, ny));
        if (willEat)
        {
            FoodsEaten++;
            SpawnFood();
        }
        else
        {
            Snake.RemoveLast();
        }
    }
}
