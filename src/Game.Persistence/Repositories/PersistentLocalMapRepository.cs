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

    public LocalMap GetOrGenerate(WorldCoord coordinate) => _inner.GetOrGenerate(coordinate);

    public bool TryGet(WorldCoord coordinate, out LocalMap map) => _inner.TryGet(coordinate, out map!);

    public void Store(LocalMap map) => _inner.Store(map);

    public InMemoryLocalMapRepository Inner => _inner;
}
