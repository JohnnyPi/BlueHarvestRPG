using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;

namespace Game.Simulation.Entities;

public sealed class EntityRegistry
{
    private readonly Overworld _overworld;
    private readonly ILocalMapRepository _localMapRepository;

    public EntityRegistry(Overworld overworld, ILocalMapRepository localMapRepository)
    {
        _overworld = overworld;
        _localMapRepository = localMapRepository;
    }

    public void EnsureDefaultsSpawned(LocalMap map)
    {
        EntityFactory.SpawnDefaults(_overworld, map);
    }
}
