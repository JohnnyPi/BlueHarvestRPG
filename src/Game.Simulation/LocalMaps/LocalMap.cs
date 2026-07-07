using Game.Simulation.Coordinates;
using Game.Simulation.Entities;

namespace Game.Simulation.LocalMaps;

public sealed class LocalMap
{
    public const int Width = 64;
    public const int Height = 64;

    public MapKey Key { get; }

    public WorldCoord WorldPosition => Key.WorldPosition;

    public int StructureInstanceId => Key.StructureInstanceId;

    public int FloorIndex => Key.FloorIndex;

    public bool IsSurface => Key.IsSurface;

    public bool IsStructureInterior => Key.IsStructureInterior;

    public TerrainId[] Terrain { get; }
    public TileFlags[] Flags { get; }
    public bool[] Explored { get; }
    public MapEntityStore Entities { get; } = new();

    public LocalMap(MapKey key)
    {
        Key = key;
        Terrain = new TerrainId[Width * Height];
        Flags = new TileFlags[Width * Height];
        Explored = new bool[Width * Height];
    }

    public LocalMap(WorldCoord worldPosition)
        : this(MapKey.Surface(worldPosition))
    {
    }

    public int GetIndex(int x, int y)
    {
        return y * Width + x;
    }

    public bool Contains(LocalCoord coord)
    {
        return coord.X >= 0 &&
               coord.Y >= 0 &&
               coord.X < Width &&
               coord.Y < Height;
    }

    public bool BlocksMovement(LocalCoord coord)
    {
        if (!Contains(coord))
        {
            return true;
        }

        int index = GetIndex(coord.X, coord.Y);
        if ((Flags[index] & TileFlags.BlocksMovement) != 0)
        {
            return true;
        }

        return Entities.BlocksMovementAt(coord);
    }

    public bool BlocksVision(LocalCoord coord)
    {
        if (!Contains(coord))
        {
            return true;
        }

        int index = GetIndex(coord.X, coord.Y);
        return (Flags[index] & TileFlags.BlocksVision) != 0;
    }

    public void SetTerrain(int x, int y, TerrainId terrain, TileFlags flags)
    {
        int index = GetIndex(x, y);
        Terrain[index] = terrain;
        Flags[index] = flags;
    }
}
