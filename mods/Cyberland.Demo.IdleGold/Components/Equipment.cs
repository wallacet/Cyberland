namespace Cyberland.Demo.IdleGold.Components;

/// <summary>Tier index per slot (0 = wood ladder start).</summary>
public struct Equipment : Cyberland.Engine.Core.Ecs.IComponent
{
    public int WeaponTier;
    public int HelmTier;
    public int ChestTier;
    public int BootsTier;
    public int RingTier;
}
