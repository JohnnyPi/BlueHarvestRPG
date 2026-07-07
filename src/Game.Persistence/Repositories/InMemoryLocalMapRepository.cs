using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;

namespace Game.Persistence.Repositories;

public sealed class InMemoryLocalMapRepository : ILocalMapRepository
{
    private readonly Dictionary<MapKey, LocalMap> _maps = new();
    private readonly Overworld _world;
    private readonly ILocalMapGenerator _generator;

    public InMemoryLocalMapRepository(Overworld world, ILocalMapGenerator generator)
    {
        _world = world;
        _generator = generator;
    }

    public LocalMap GetOrGenerate(MapKey key)
    {
        if (_maps.TryGetValue(key, out LocalMap? map))
        {
            return map;
        }

        map = _generator.Generate(_world, key);
        if (key.IsSurface)
        {
            EntityFactory.SpawnDefaults(_world, map);
        }

        _maps.Add(key, map);
        return map;
    }

    public bool TryGet(MapKey key, out LocalMap map) => _maps.TryGetValue(key, out map!);

    public void Store(LocalMap map) => _maps[map.Key] = map;

    public IReadOnlyDictionary<MapKey, LocalMap> Maps => _maps;
}
