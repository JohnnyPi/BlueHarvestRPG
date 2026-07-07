using Game.Generation.Passes;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;

namespace Game.Generation.Passes;

public interface IGenerationPass
{
    void Execute(LocalMap map, LocalGenerationContext context);
}
