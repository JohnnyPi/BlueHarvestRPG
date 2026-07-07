using System.Runtime.InteropServices;
using Game.Simulation.Coordinates;
using Game.Simulation.World.Island;

namespace Game.Simulation.World;

public sealed class Overworld
{
    public const int DefaultSize = 512;

    public int Width { get; }
    public int Height { get; }
    public ulong Seed { get; }
    public IslandPlan? IslandPlan { get; set; }

    public bool[] Explored { get; }

    private readonly WorldCell[] _cells;
    private readonly List<EdgeConnection>[] _edgeConnections;

    public Overworld(int width, int height, ulong seed)
    {
        Width = width;
        Height = height;
        Seed = seed;
        _cells = new WorldCell[width * height];
        Explored = new bool[width * height];
        _edgeConnections = new List<EdgeConnection>[width * height];

        for (int i = 0; i < _edgeConnections.Length; i++)
        {
            _edgeConnections[i] = [];
        }
    }

    public ref WorldCell GetCell(WorldCoord coord)
    {
        if (!Contains(coord))
        {
            throw new ArgumentOutOfRangeException(nameof(coord));
        }

        return ref _cells[GetIndex(coord)];
    }

    public WorldCell GetCellValue(WorldCoord coord)
    {
        return GetCell(coord);
    }

    public ReadOnlySpan<EdgeConnection> GetEdgeConnections(WorldCoord coord)
    {
        if (!Contains(coord))
        {
            throw new ArgumentOutOfRangeException(nameof(coord));
        }

        return CollectionsMarshal.AsSpan(_edgeConnections[GetIndex(coord)]);
    }

    public void AddEdgeConnection(WorldCoord coord, EdgeConnection connection)
    {
        if (!Contains(coord))
        {
            throw new ArgumentOutOfRangeException(nameof(coord));
        }

        int index = GetIndex(coord);
        _edgeConnections[index].Add(connection);
        _cells[index].ConnectionFlags |= connection.ToFlag();
    }

    public bool Contains(WorldCoord coord)
    {
        return coord.X >= 0 &&
               coord.Y >= 0 &&
               coord.X < Width &&
               coord.Y < Height;
    }

    public int GetIndex(WorldCoord coord)
    {
        return coord.Y * Width + coord.X;
    }

    public ReadOnlySpan<WorldCell> Cells => _cells;
}
