using Cyberland.Demo.IdleGold.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Localization;

namespace Cyberland.Demo.IdleGold.Systems;

/// <summary>Passive gold from rates and time-based Luck procs (uses variable <c>deltaSeconds</c>).</summary>
public sealed class SimulationSystem : ISingletonSystem, ISingletonLateUpdate
{
    private readonly LocalizationManager _loc;

    public SimulationSystem(LocalizationManager loc) => _loc = loc;

    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SessionTag>();

    public void OnSingletonStart(in SingletonEntity stateRow)
    {
        _ = ref stateRow.Get<Wallet>();
        _ = ref stateRow.Get<EventLog>();
    }

    public void OnSingletonLateUpdate(in SingletonEntity row, float deltaSeconds)
    {
        ref var wallet = ref row.Get<Wallet>();
        ref var sources = ref row.Get<Sources>();
        ref var stats = ref row.Get<Stats>();
        ref var eq = ref row.Get<Equipment>();
        ref var rng = ref row.Get<RngState>();

        var rate = Economy.TotalGoldPerSecond(ref sources, in stats, in eq);
        var earned = rate * deltaSeconds;
        wallet.Gold += earned;
        wallet.LifetimeEarned += earned;

        TickLuck(row.World, row.Entity, ref wallet, ref stats, ref rng, deltaSeconds);
    }

    private void TickLuck(World world, EntityId session, ref Wallet wallet, ref Stats stats, ref RngState rng,
        float deltaSeconds)
    {
        rng.LuckAccumulatorSec += deltaSeconds;
        var luckLevel = stats.Luck;

        while (rng.LuckAccumulatorSec >= GameBalance.LuckProcIntervalSec)
        {
            rng.LuckAccumulatorSec -= GameBalance.LuckProcIntervalSec;

            var chance = Math.Min(
                GameBalance.LuckProcChanceCap,
                GameBalance.LuckProcChanceBase + GameBalance.LuckProcChancePerLuck * luckLevel);

            if (ProcRng.Next01(ref rng.State) >= chance)
                continue;

            var bonus = GameBalance.LuckProcBonusGoldBase + GameBalance.LuckProcBonusPerLuck * luckLevel;
            wallet.Gold += bonus;
            LogBook.Append(world, session, string.Format(_loc.Get("idlegold.log.luck_proc"), bonus.ToString("F0")));
        }
    }
}
