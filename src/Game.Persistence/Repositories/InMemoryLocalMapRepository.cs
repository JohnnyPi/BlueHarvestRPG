using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;

namespace Game.Persistence.Repositories;

public sealed class InMemoryLocalMapRepository : ILocalMapRepository
{
    private readonly Dictionary<WorldCoord, LocalMap> _maps = new();
    private readonly Overworld _world;
    private readonly ILocalMapGenerator _generator;

    public InMemoryLocalMapRepository(Overworld world, ILocalMapGenerator generator)
    {
        _world = world;
        _generator = generator;
    }

    public LocalMap GetOrGenerate(WorldCoord coordinate)
    {
        if (_maps.TryGetValue(coordinate, out LocalMap? map))
        {
            return map;
        }

        map = _generator.Generate(_world, coordinate);
        EntityFactory.SpawnDefaults(_world, map);
        _maps.Add(coordinate, map);
        return map;
    }

    public bool TryGet(WorldCoord coordinate, out LocalMap map)
    {
        return _maps.TryGetValue(coordinate, out map!);
    }

    public void Store(LocalMap map)
    {
        _maps[map.WorldPosition] = map;
    }

    public IReadOnlyDictionary<WorldCoord, LocalMap> Maps => _maps;
}
