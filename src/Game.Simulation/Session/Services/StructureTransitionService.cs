using Game.Simulation.World.Island;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Items;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;
using Game.Simulation.World;

namespace Game.Simulation.Session.Services;

public sealed class StructureTransitionService
{
    private readonly ActiveMapSwitchService _mapSwitch = new();

    public bool CanUseTransition(
        GameSession session,
        int x,
        int y,
        StructureBlueprintCatalog blueprintCatalog,
        out TileTransition transition,
        TileTransitionKind? preferredKind = null)
    {
        transition = default;

        if (session.ViewMode != GameViewMode.LocalMap || session.ActiveLocalMap is null)
        {
            return false;
        }

        var position = new LocalCoord(x, y);
        if (!TileTransitionResolver.TryResolveAt(
                session.Overworld,
                session.ActiveLocalMap,
                position,
                blueprintCatalog,
                out transition,
                preferredKind))
        {
            return false;
        }

        if (transition.RequiresRope && !HasRope(session))
        {
            return false;
        }

        return true;
    }

    public bool TryApplyTransition(
        GameSession session,
        TileTransition transition,
        StructureBlueprintCatalog blueprintCatalog)
    {
        if (transition.RequiresRope && !HasRope(session))
        {
            session.MessageLog.Add("You need rope to descend from here.");
            return false;
        }

        LocalMap destination = session.LocalMapRepository.GetOrGenerate(transition.Destination);
        LocalCoord landing = WalkabilityHelper.FindNearestWalkable(destination, transition.DestinationLocal);
        if (destination.BlocksMovement(landing))
        {
            return false;
        }

        if (transition.Kind == TileTransitionKind.EnterStructure)
        {
            session.SurfaceReturnKey = session.ActiveLocalMap?.Key ?? MapKey.Surface(session.PlayerWorldPosition);
            session.SurfaceReturnPosition = session.PlayerLocalPosition;
        }
        else if (transition.Kind is TileTransitionKind.ExitStructure or TileTransitionKind.RopeDescent)
        {
            session.SurfaceReturnKey = null;
            session.SurfaceReturnPosition = null;
        }

        _mapSwitch.SwitchTo(session, destination, landing, markSurfaceLocalChanges: true);
        session.MarkRenderDirty();

        session.MessageLog.Add(DescribeTransition(transition));
        return true;
    }

    private static bool HasRope(GameSession session)
    {
        return session.Inventory.Stacks.Any(stack => stack.ItemId == ItemId.Rope && stack.Count > 0);
    }

    private static string DescribeTransition(TileTransition transition)
    {
        return transition.Kind switch
        {
            TileTransitionKind.EnterStructure => "Entered the building.",
            TileTransitionKind.ExitStructure => "Exited to the island.",
            TileTransitionKind.StairsUp => "Climbed upstairs.",
            TileTransitionKind.StairsDown => "Went downstairs.",
            TileTransitionKind.RopeDescent => "Rappelled down with rope.",
            _ => "Moved."
        };
    }
}
