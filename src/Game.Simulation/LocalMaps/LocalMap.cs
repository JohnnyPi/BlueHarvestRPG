using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Perception;
using Game.Simulation.World;

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
    public NoiseField Noise { get; } = new();
    public ScentField Scent { get; } = new();
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

    public bool ReducesVision(LocalCoord coord)
    {
        if (!Contains(coord))
        {
            return false;
        }

        int index = GetIndex(coord.X, coord.Y);
        if ((Flags[index] & TileFlags.ReducesVision) != 0)
        {
            return true;
        }

        TerrainId terrain = Terrain[index];
        return terrain == TerrainId.Grass;
    }

    public void SetTerrain(int x, int y, TerrainId terrain, TileFlags flags)
    {
        int index = GetIndex(x, y);
        Terrain[index] = terrain;
        Flags[index] = flags;
    }
}
