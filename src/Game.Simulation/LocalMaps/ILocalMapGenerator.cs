using Game.Simulation.Coordinates;
using Game.Simulation.World;

namespace Game.Simulation.LocalMaps;

public interface ILocalMapGenerator
{
    LocalMap Generate(Overworld world, WorldCoord coordinate);
}
