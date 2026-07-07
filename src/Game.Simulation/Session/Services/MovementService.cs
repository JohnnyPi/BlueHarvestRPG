using Game.Simulation.Combat;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Factions;
using Game.Simulation.LocalMaps;
using Game.Simulation.Pathfinding;
using Game.Simulation.Session;
using Game.Simulation.World;

namespace Game.Simulation.Session.Services;

public sealed class MovementService
{
    private readonly CombatResolver _combat = new();
    private readonly MapTransitionService _transitions;

    public MovementService(MapTransitionService? transitions = null)
    {
        _transitions = transitions ?? new MapTransitionService();
    }

    public bool CanEnterOverworldCell(Overworld overworld, WorldCoord coord)
    {
        if (!overworld.Contains(coord))
        {
            return false;
        }

        BiomeId biome = overworld.GetCellValue(coord).Biome;
        return BiomeTraversal.IsPassable(biome);
    }

    public int GetOverworldMoveCost(Overworld overworld, WorldCoord from, WorldCoord to)
    {
        return OverworldTravelCost.GetStepCost(overworld, from, to);
    }

    public bool TryMoveOverworld(GameSession session, int deltaX, int deltaY)
    {
        if (session.ViewMode != GameViewMode.Overworld)
        {
            return false;
        }

        var next = new WorldCoord(
            session.PlayerWorldPosition.X + deltaX,
            session.PlayerWorldPosition.Y + deltaY);

        if (!session.CanEnterOverworldCoord(next))
        {
            return false;
        }

        session.PlayerWorldPosition = next;
        session.SyncPlayerEntityPosition();
        session.MarkVisibilityDirty();
        session.RevealOverworldAroundPlayer();
        return true;
    }

    public bool TryMoveLocal(GameSession session, int deltaX, int deltaY)
    {
        if (session.ViewMode != GameViewMode.LocalMap || session.ActiveLocalMap is null)
        {
            return false;
        }

        var next = new LocalCoord(
            session.PlayerLocalPosition.X + deltaX,
            session.PlayerLocalPosition.Y + deltaY);

        if (!session.ActiveLocalMap.Contains(next))
        {
            if (MapTransitionResolver.TryResolve(
                    session.Overworld,
                    session.PlayerWorldPosition,
                    session.PlayerLocalPosition,
                    deltaX,
                    deltaY,
                    out MapTransition transition))
            {
                return _transitions.TryTransitionToMap(session, transition);
            }

            return false;
        }

        Entity? occupant = session.ActiveLocalMap.Entities.GetAt(next);
        if (occupant is not null && occupant.IsActive && occupant.Id != EntityId.Player)
        {
            if (FactionRelations.IsHostile(FactionId.Player, occupant.Faction))
            {
                return _combat.TryAttack(session, session.PlayerEntity, occupant);
            }

            if (occupant.BlocksMovement)
            {
                return false;
            }
        }

        if (session.ActiveLocalMap.BlocksMovement(next))
        {
            return false;
        }

        session.PlayerLocalPosition = next;
        session.SyncPlayerEntityPosition();
        session.MarkVisibilityDirty();
        return true;
    }

    public void AdvanceMovement(GameSession session)
    {
        if (session.MovementPath.Count == 0)
        {
            return;
        }

        (int X, int Y) next = session.MovementPath.Peek();

        if (session.ViewMode == GameViewMode.Overworld)
        {
            var coord = new WorldCoord(next.X, next.Y);
            if (!session.CanEnterOverworldCoord(coord))
            {
                session.ClearMovement();
                return;
            }

            session.PlayerWorldPosition = coord;
            session.MovementPath.Dequeue();
            session.SyncPlayerEntityPosition();
            session.RevealOverworldAroundPlayer();
        }
        else if (session.ActiveLocalMap is not null)
        {
            int deltaX = next.X - session.PlayerLocalPosition.X;
            int deltaY = next.Y - session.PlayerLocalPosition.Y;
            if (!TryMoveLocal(session, deltaX, deltaY))
            {
                session.ClearMovement();
                return;
            }

            session.MovementPath.Dequeue();
        }
        else
        {
            session.ClearMovement();
            return;
        }

        if (session.MovementPath.Count == 0 && session.EnterOnArrival)
        {
            session.EnterOnArrival = false;
            if (session.ViewMode == GameViewMode.Overworld)
            {
                session.EnterWorldCell();
            }
        }
        else if (session.MovementPath.Count == 0 && session.TransitionOnArrival)
        {
            session.TransitionOnArrival = false;
            _transitions.TryTransitionAcrossEdge(
                session,
                session.TransitionEdgeOnArrival,
                session.TransitionBorderX,
                session.TransitionBorderY);
        }
    }

    public List<(int X, int Y)> BuildPathTo(GameSession session, int targetX, int targetY)
    {
        if (session.ViewMode == GameViewMode.Overworld)
        {
            Overworld overworld = session.Overworld;
            return GridPathfinder.FindPath(
                session.PlayerWorldPosition.X,
                session.PlayerWorldPosition.Y,
                targetX,
                targetY,
                overworld.Width,
                overworld.Height,
                (x, y) => !session.CanEnterOverworldCoord(new WorldCoord(x, y)),
                stepCost: (fromX, fromY, toX, toY) => OverworldTravelCost.GetStepCost(
                    overworld,
                    new WorldCoord(fromX, fromY),
                    new WorldCoord(toX, toY)));
        }

        if (session.ActiveLocalMap is null)
        {
            return [];
        }

        LocalMap map = session.ActiveLocalMap;
        return GridPathfinder.FindPath(
            session.PlayerLocalPosition.X,
            session.PlayerLocalPosition.Y,
            targetX,
            targetY,
            LocalMap.Width,
            LocalMap.Height,
            (x, y) => map.BlocksMovement(new LocalCoord(x, y)),
            (x, y) => GridPathfinder.GetTerrainMoveCost(map.Terrain[map.GetIndex(x, y)]));
    }
}
