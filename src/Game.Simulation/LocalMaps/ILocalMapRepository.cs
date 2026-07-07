using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;

namespace Game.Simulation.LocalMaps;

public interface ILocalMapRepository
{
    LocalMap GetOrGenerate(MapKey key);

    bool TryGet(MapKey key, out LocalMap map);

    void Store(LocalMap map);
}

public static class LocalMapRepositoryExtensions
{
    public static LocalMap GetOrGenerateSurface(this ILocalMapRepository repository, WorldCoord coordinate) =>
        repository.GetOrGenerate(MapKey.Surface(coordinate));

    public static bool TryGetSurface(this ILocalMapRepository repository, WorldCoord coordinate, out LocalMap map) =>
        repository.TryGet(MapKey.Surface(coordinate), out map);
}
