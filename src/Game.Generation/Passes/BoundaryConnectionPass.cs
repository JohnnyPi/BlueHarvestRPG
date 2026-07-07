using Game.Generation.Passes;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;

namespace Game.Generation.Passes;

public sealed class BoundaryConnectionPass : IGenerationPass
{
    public void Execute(LocalMap map, LocalGenerationContext context)
    {
        foreach (EdgeConnection connection in context.Connections)
        {
            if (connection.Type == ConnectionType.Road)
            {
                ApplyRoadConnection(map, connection);
            }
            else if (connection.Type == ConnectionType.River)
            {
                ApplyRiverConnection(map, connection);
            }
        }
    }

    private static void ApplyRoadConnection(LocalMap map, EdgeConnection connection)
    {
        StampRoadCorridor(map, connection);
    }

    private static void ApplyRiverConnection(LocalMap map, EdgeConnection connection)
    {
        StampRiverCorridor(map, connection);
    }

    private static void StampRoadCorridor(LocalMap map, EdgeConnection connection)
    {
        if (connection.Edge is Direction.East or Direction.West)
        {
            for (int y = connection.LocalOffset; y < connection.LocalOffset + connection.Width; y++)
            {
                for (int x = 0; x < LocalMap.Width; x++)
                {
                    StampRoad(map, x, y);
                }
            }
        }
        else
        {
            for (int x = connection.LocalOffset; x < connection.LocalOffset + connection.Width; x++)
            {
                for (int y = 0; y < LocalMap.Height; y++)
                {
                    StampRoad(map, x, y);
                }
            }
        }
    }

    private static void StampRoad(LocalMap map, int x, int y)
    {
        map.SetTerrain(x, y, TerrainId.Road, TileFlags.None);
    }

    private static void StampRiverCorridor(LocalMap map, EdgeConnection connection)
    {
        if (connection.Edge is Direction.East or Direction.West)
        {
            for (int y = connection.LocalOffset; y < connection.LocalOffset + connection.Width; y++)
            {
                for (int x = 0; x < LocalMap.Width; x++)
                {
                    StampRiver(map, x, y);
                }
            }
        }
        else
        {
            for (int x = connection.LocalOffset; x < connection.LocalOffset + connection.Width; x++)
            {
                for (int y = 0; y < LocalMap.Height; y++)
                {
                    StampRiver(map, x, y);
                }
            }
        }
    }

    private static void StampRiver(LocalMap map, int x, int y)
    {
        if (x < 0 || y < 0 || x >= LocalMap.Width || y >= LocalMap.Height)
        {
            return;
        }

        int index = map.GetIndex(x, y);
        TerrainId terrain = map.Terrain[index];
        if (terrain is TerrainId.Road or TerrainId.Door or TerrainId.Wall or TerrainId.Concrete)
        {
            return;
        }

        map.SetTerrain(x, y, TerrainId.ShallowWater, TileFlags.ContainsWater);
    }
}
