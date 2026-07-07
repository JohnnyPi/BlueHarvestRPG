using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;

namespace Game.Simulation.World;

public static class MapTransitionResolver
{
    public static bool TryResolve(
        Overworld overworld,
        WorldCoord currentWorld,
        LocalCoord currentLocal,
        int deltaX,
        int deltaY,
        out MapTransition transition)
    {
        transition = default;

        int nextX = currentLocal.X + deltaX;
        int nextY = currentLocal.Y + deltaY;

        if (nextX >= 0 &&
            nextX < LocalMap.Width &&
            nextY >= 0 &&
            nextY < LocalMap.Height)
        {
            return false;
        }

        WorldCoord destinationWorld;
        LocalCoord destinationLocal;

        if (nextX < 0)
        {
            destinationWorld = new WorldCoord(currentWorld.X - 1, currentWorld.Y);
            destinationLocal = new LocalCoord(LocalMap.Width - 1, currentLocal.Y);
        }
        else if (nextX >= LocalMap.Width)
        {
            destinationWorld = new WorldCoord(currentWorld.X + 1, currentWorld.Y);
            destinationLocal = new LocalCoord(0, currentLocal.Y);
        }
        else if (nextY < 0)
        {
            destinationWorld = new WorldCoord(currentWorld.X, currentWorld.Y - 1);
            destinationLocal = new LocalCoord(currentLocal.X, LocalMap.Height - 1);
        }
        else
        {
            destinationWorld = new WorldCoord(currentWorld.X, currentWorld.Y + 1);
            destinationLocal = new LocalCoord(currentLocal.X, 0);
        }

        if (!overworld.Contains(destinationWorld))
        {
            return false;
        }

        transition = new MapTransition(destinationWorld, destinationLocal);
        return true;
    }
}
