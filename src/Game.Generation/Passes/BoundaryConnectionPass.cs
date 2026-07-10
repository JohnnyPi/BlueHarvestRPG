using Game.Generation.Passes;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;

namespace Game.Generation.Passes;

public sealed class BoundaryConnectionPass : IGenerationPass
{
    private const int ConnectorLength = 6;

    public void Execute(LocalMap map, LocalGenerationContext context)
    {
        foreach (EdgeConnection connection in context.Connections)
        {
            if (connection.Type == ConnectionType.Road)
            {
                if (context.IslandPlan?.RoadGraph.GlobalPathTiles.Count > 0)
                {
                    continue;
                }

                ApplyRoadConnection(map, connection);
            }
            else if (connection.Type == ConnectionType.River)
            {
                if (context.IslandPlan?.RiverGraph.GlobalRiverTiles.Count > 0)
                {
                    continue;
                }

                ApplyRiverConnection(map, connection);
            }
        }
    }

    private static void ApplyRoadConnection(LocalMap map, EdgeConnection connection)
    {
        StampConnector(map, connection, StampRoad);
    }

    private static void ApplyRiverConnection(LocalMap map, EdgeConnection connection)
    {
        StampConnector(map, connection, StampRiver);
    }

    private static void StampConnector(LocalMap map, EdgeConnection connection, Action<LocalMap, int, int> stamp)
    {
        if (connection.Edge is Direction.East or Direction.West)
        {
            int xStart = connection.Edge == Direction.West ? 0 : LocalMap.Width - ConnectorLength;
            for (int y = connection.LocalOffset; y < connection.LocalOffset + connection.Width; y++)
            {
                for (int x = xStart; x < xStart + ConnectorLength; x++)
                {
                    stamp(map, x, y);
                }
            }
        }
        else
        {
            int yStart = connection.Edge == Direction.North ? 0 : LocalMap.Height - ConnectorLength;
            for (int x = connection.LocalOffset; x < connection.LocalOffset + connection.Width; x++)
            {
                for (int y = yStart; y < yStart + ConnectorLength; y++)
                {
                    stamp(map, x, y);
                }
            }
        }
    }

    private static void StampRoad(LocalMap map, int x, int y)
    {
        if (x < 0 || y < 0 || x >= LocalMap.Width || y >= LocalMap.Height)
        {
            return;
        }

        int index = map.GetIndex(x, y);
        TerrainId terrain = map.Terrain[index];
        if (terrain == TerrainId.ShallowFord)
        {
            return;
        }

        if (terrain is TerrainId.Wall or TerrainId.InteriorWall or TerrainId.Door or TerrainId.Fence
            or TerrainId.Floor or TerrainId.StructureExit or TerrainId.Dock)
        {
            return;
        }

        map.SetTerrain(x, y, TerrainId.Road, TileFlags.None);
    }

    private static void StampRiver(LocalMap map, int x, int y)
    {
        if (x < 0 || y < 0 || x >= LocalMap.Width || y >= LocalMap.Height)
        {
            return;
        }

        int index = map.GetIndex(x, y);
        TerrainId terrain = map.Terrain[index];
        if (terrain is TerrainId.Road)
        {
            map.SetTerrain(x, y, TerrainId.ShallowFord, TileFlags.ContainsWater);
            return;
        }

        if (terrain is TerrainId.Door or TerrainId.Wall or TerrainId.Concrete)
        {
            return;
        }

        map.SetTerrain(x, y, TerrainId.ShallowFord, TileFlags.ContainsWater);
    }
}
