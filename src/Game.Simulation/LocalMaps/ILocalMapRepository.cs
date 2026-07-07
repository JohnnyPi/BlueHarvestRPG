using Game.Simulation.Coordinates;

namespace Game.Simulation.LocalMaps;

public interface ILocalMapRepository
{
    LocalMap GetOrGenerate(WorldCoord coordinate);

    bool TryGet(WorldCoord coordinate, out LocalMap map);

    void Store(LocalMap map);
}
