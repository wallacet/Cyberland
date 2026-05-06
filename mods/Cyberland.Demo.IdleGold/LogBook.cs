using Cyberland.Demo.IdleGold.Components;
using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.IdleGold;

/// <summary>Session log lines with a small cap to avoid unbounded growth during long AFK.</summary>
public static class LogBook
{
    public const int MaxLines = 120;

    public static void Append(World world, EntityId session, string line)
    {
        ref var log = ref world.Get<EventLog>(session);
        log.Lines ??= new List<string>();
        log.Lines.Add(line);
        while (log.Lines.Count > MaxLines)
            log.Lines.RemoveAt(0);
    }

    public static string BuildText(World world, EntityId session)
    {
        var log = world.Get<EventLog>(session);
        if (log.Lines is null || log.Lines.Count == 0)
            return string.Empty;
        return string.Join('\n', log.Lines);
    }
}
