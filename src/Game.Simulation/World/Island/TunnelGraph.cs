namespace Game.Simulation.World.Island;

public sealed class TunnelGraph
{
    public List<TunnelNode> Nodes { get; } = [];
    public List<TunnelSegment> Segments { get; } = [];

    public HashSet<(int X, int Y)> AllTunnelTiles { get; } = [];
    public HashSet<(int X, int Y)> CavernTiles { get; } = [];
}
