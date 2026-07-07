using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;

namespace Game.Persistence.Repositories;

public sealed class PersistentLocalMapRepository : ILocalMapRepository
{
    private readonly InMemoryLocalMapRepository _inner;

    public PersistentLocalMapRepository(Overworld world, ILocalMapGenerator generator)
    {
        _inner = new InMemoryLocalMapRepository(world, generator);
    }

    public LocalMap GetOrGenerate(MapKey key) => _inner.GetOrGenerate(key);

    public bool TryGet(MapKey key, out LocalMap map) => _inner.TryGet(key, out map!);

    public void Store(LocalMap map) => _inner.Store(map);

    public InMemoryLocalMapRepository Inner => _inner;
}
