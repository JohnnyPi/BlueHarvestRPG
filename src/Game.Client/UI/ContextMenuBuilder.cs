using Game.Content.Definitions;
using Game.Simulation.Input;
using Game.Simulation.Session;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Client.UI;

public static class ContextMenuBuilder
{
    public static List<ContextMenuItem> Build(
        GameViewMode viewMode,
        int tileX,
        int tileY,
        ContextMenusDefinition menus,
        Overworld overworld,
        GameSession session,
        StructureBlueprintCatalog blueprintCatalog)
    {
        IReadOnlyList<ContextMenuEntry> source = viewMode == GameViewMode.Overworld
            ? menus.Overworld
            : menus.LocalMap;

        var items = new List<ContextMenuItem>();
        foreach (ContextMenuEntry entry in source)
        {
            if (!IsActionAvailable(session, viewMode, tileX, tileY, entry.Intent, blueprintCatalog))
            {
                continue;
            }

            items.Add(new ContextMenuItem
            {
                Label = entry.Label,
                Intent = entry.Intent
            });
        }

        if (viewMode == GameViewMode.LocalMap)
        {
            AppendBorderTransitions(items, tileX, tileY, overworld, session);
        }

        return items;
    }

    private static bool IsActionAvailable(
        GameSession session,
        GameViewMode viewMode,
        int tileX,
        int tileY,
        string intent,
        StructureBlueprintCatalog blueprintCatalog)
    {
        if (!Enum.TryParse(intent, ignoreCase: true, out GameIntent gameIntent))
        {
            return true;
        }

        return gameIntent switch
        {
            GameIntent.HarvestAtSelected => session.CanHarvestAt(tileX, tileY),
            GameIntent.RemoveTerrainAtSelected => session.CanRemoveTreeTerrainAt(tileX, tileY),
            GameIntent.EnterSelected => viewMode == GameViewMode.Overworld && session.CanEnterOverworldTile(tileX, tileY),
            GameIntent.EnterStructure => session.CanEnterStructureAt(tileX, tileY, blueprintCatalog),
            GameIntent.ExitStructure => session.CanExitStructureAt(tileX, tileY, blueprintCatalog),
            GameIntent.UseStairsUp => session.CanUseTileTransition(tileX, tileY, blueprintCatalog, TileTransitionKind.StairsUp),
            GameIntent.UseStairsDown => session.CanUseTileTransition(tileX, tileY, blueprintCatalog, TileTransitionKind.StairsDown),
            GameIntent.UseRopeDescent => session.CanUseRopeAt(tileX, tileY, blueprintCatalog),
            _ => true
        };
    }

    private static void AppendBorderTransitions(
        List<ContextMenuItem> items,
        int tileX,
        int tileY,
        Overworld overworld,
        GameSession session)
    {
        if (!MapBorderHelper.IsBorderTile(tileX, tileY))
        {
            return;
        }

        List<Direction> edges = MapBorderHelper.GetTransitionEdges(tileX, tileY).ToList();
        bool includeDirection = edges.Count > 1;

        foreach (Direction edge in edges)
        {
            if (!session.WouldTransitionAcrossEdge(edge, tileX, tileY))
            {
                continue;
            }

            items.Insert(0, new ContextMenuItem
            {
                Label = MapBorderHelper.FormatTransitionLabel(edge, includeDirection),
                Intent = TransitionIntentFor(edge).ToString()
            });
        }
    }

    private static GameIntent TransitionIntentFor(Direction edge)
    {
        return edge switch
        {
            Direction.North => GameIntent.TransitionBorderNorth,
            Direction.East => GameIntent.TransitionBorderEast,
            Direction.South => GameIntent.TransitionBorderSouth,
            Direction.West => GameIntent.TransitionBorderWest,
            _ => GameIntent.None
        };
    }
}
