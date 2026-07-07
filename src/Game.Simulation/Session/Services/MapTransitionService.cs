using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;
using Game.Simulation.World;

namespace Game.Simulation.Session.Services;

public sealed class MapTransitionService
{
    public bool WouldTransitionAcrossEdge(
        GameSession session,
        Direction edge,
        int borderLocalX,
        int borderLocalY)
    {
        if (session.ViewMode != GameViewMode.LocalMap || session.ActiveLocalMap?.IsSurface != true)
        {
            return false;
        }

        if (!MapBorderHelper.TryResolveBorderTransition(
                session.Overworld,
                session.PlayerWorldPosition,
                edge,
                borderLocalX,
                borderLocalY,
                out MapTransition transition))
        {
            return false;
        }

        if (session.LocalMapRepository.TryGetSurface(transition.DestinationWorld, out LocalMap destination))
        {
            return HasWalkableLanding(destination, transition.DestinationLocal);
        }

        return true;
    }

    public bool CanTransitionAcrossEdge(
        GameSession session,
        Direction edge,
        int borderLocalX,
        int borderLocalY)
    {
        if (session.ViewMode != GameViewMode.LocalMap || session.ActiveLocalMap?.IsSurface != true)
        {
            return false;
        }

        if (!MapBorderHelper.TryResolveBorderTransition(
                session.Overworld,
                session.PlayerWorldPosition,
                edge,
                borderLocalX,
                borderLocalY,
                out MapTransition transition))
        {
            return false;
        }

        LocalMap destination = session.LocalMapRepository.GetOrGenerateSurface(transition.DestinationWorld);
        return HasWalkableLanding(destination, transition.DestinationLocal);
    }

    public bool TryTransitionAcrossEdge(
        GameSession session,
        Direction edge,
        int borderLocalX,
        int borderLocalY)
    {
        if (session.ViewMode != GameViewMode.LocalMap || session.ActiveLocalMap is null)
        {
            return false;
        }

        if (session.PlayerLocalPosition.X != borderLocalX || session.PlayerLocalPosition.Y != borderLocalY)
        {
            return false;
        }

        if (!MapBorderHelper.TryResolveBorderTransition(
                session.Overworld,
                session.PlayerWorldPosition,
                edge,
                borderLocalX,
                borderLocalY,
                out MapTransition transition))
        {
            return false;
        }

        return TryTransitionToMap(session, transition);
    }

    public bool TryTransitionToMap(GameSession session, MapTransition transition)
    {
        if (session.ActiveLocalMap is null)
        {
            return false;
        }

        LocalMap destination = session.LocalMapRepository.GetOrGenerateSurface(transition.DestinationWorld);
        LocalCoord landing = WalkabilityHelper.FindNearestWalkable(destination, transition.DestinationLocal);
        if (destination.BlocksMovement(landing))
        {
            return false;
        }

        session.LocalMapRepository.Store(session.ActiveLocalMap);
        ref WorldCell currentCell = ref session.Overworld.GetCell(session.ActiveLocalMap.WorldPosition);
        currentCell.HasLocalChanges = true;

        session.PlayerWorldPosition = transition.DestinationWorld;
        session.PlayerLocalPosition = landing;
        session.ActiveLocalMap = destination;
        session.SyncPlayerEntityPosition();
        session.MarkVisibilityDirty();

        return true;
    }

    private static bool HasWalkableLanding(LocalMap map, LocalCoord intended)
    {
        LocalCoord landing = WalkabilityHelper.FindNearestWalkable(map, intended);
        return map.Contains(landing) && !map.BlocksMovement(landing);
    }
}
