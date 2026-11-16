using System.Collections.Generic;

/// <summary>
/// Scene-local registry used by police detection and the minimap. Components
/// register themselves on enable, so gameplay code does not need to scan all
/// NPCs every frame.
/// </summary>
public static class PoliceHeatRegistry
{
    private static readonly HashSet<PoliceBehavior> police = new HashSet<PoliceBehavior>();
    private static readonly HashSet<PoliceHeatController> players = new HashSet<PoliceHeatController>();
    private static readonly HashSet<BuyerBehavior> buyers = new HashSet<BuyerBehavior>();

    public static IEnumerable<PoliceBehavior> ActivePolice => police;
    public static IEnumerable<PoliceHeatController> ActivePlayers => players;
    public static IEnumerable<BuyerBehavior> ActiveBuyers => buyers;

    public static void Register(PoliceBehavior value)
    {
        if (value != null)
            police.Add(value);
    }

    public static void Unregister(PoliceBehavior value)
    {
        if (value != null)
            police.Remove(value);
    }

    public static void Register(PoliceHeatController value)
    {
        if (value != null)
            players.Add(value);
    }

    public static void Unregister(PoliceHeatController value)
    {
        if (value != null)
            players.Remove(value);
    }

    public static void Register(BuyerBehavior value)
    {
        if (value != null)
            buyers.Add(value);
    }

    public static void Unregister(BuyerBehavior value)
    {
        if (value != null)
            buyers.Remove(value);
    }
}
