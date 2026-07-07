using Game.Generation.LocalMaps;
using Game.Persistence.Repositories;
using Game.Simulation;
using Game.Simulation.Coordinates;
using Game.Simulation.Session;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

internal static class TestOverworldFactory
{
    public static SimulationHost CreatePlainsHost(int size = 32, ulong seed = 1)
    {
        var overworld = CreatePlainsOverworld(size, seed);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        var host = new SimulationHost(overworld, session, repository) { IsNewGame = true };
        host.Initialize();
        return host;
    }

    public static Overworld CreatePlainsOverworld(int size, ulong seed)
    {
        var overworld = new Overworld(size, size, seed);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                ref WorldCell cell = ref overworld.GetCell(new WorldCoord(x, y));
                cell.Elevation = 0.55f;
                cell.Moisture = 0.45f;
                cell.Temperature = 0.55f;
                cell.Biome = BiomeId.Plains;
            }
        }

        return overworld;
    }
}
