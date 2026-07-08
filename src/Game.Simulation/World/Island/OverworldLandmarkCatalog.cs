using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.Rendering;
using Game.Simulation.Scenarios;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.World.Island;

public static class OverworldLandmarkCatalog
{
    private static readonly (IslandCellRole Role, string Name)[] SiteRoles =
    [
        (IslandCellRole.Paddock, "Paddock"),
        (IslandCellRole.Tunnel, "Tunnel entrance"),
        (IslandCellRole.Cavern, "Cavern"),
    ];

    public static string GetName(IslandPlan plan, int x, int y)
    {
        if (!plan.Contains(x, y))
        {
            return string.Empty;
        }

        if (plan.VisitorCenterCell.X == x && plan.VisitorCenterCell.Y == y)
        {
            return "Visitor Center";
        }

        ref IslandCellData cell = ref plan.GetCell(x, y);
        foreach (StructurePlacement structure in plan.Structures)
        {
            if (CoordinateMath.OverlapsCell(
                    structure.GlobalOriginX,
                    structure.GlobalOriginY,
                    structure.Width,
                    structure.Height,
                    new WorldCoord(x, y)))
            {
                return GetStructureName(structure.Type);
            }
        }

        foreach (RuinSite ruin in plan.RuinSites)
        {
            if (CoordinateMath.OverlapsCell(
                    ruin.GlobalOriginX,
                    ruin.GlobalOriginY,
                    ruin.Width,
                    ruin.Height,
                    new WorldCoord(x, y)))
            {
                return ruin.Kind == RuinKind.WarFortification ? "Fortification" : "Ruins";
            }
        }

        foreach ((IslandCellRole role, string name) in SiteRoles)
        {
            if (cell.Role.HasFlag(role))
            {
                return name;
            }
        }

        foreach (VolcanicSite site in plan.VolcanicSites)
        {
            if (site.X == x && site.Y == y)
            {
                return site.Origin switch
                {
                    VolcanicOrigin.MantlePlume => "Hotspot vent",
                    VolcanicOrigin.SubductionArc => "Volcanic arc",
                    VolcanicOrigin.RiftVolcano => "Rift volcano",
                    VolcanicOrigin.CollisionVolcanism => "Collision peak",
                    _ => "Volcanic vent"
                };
            }
        }

        return string.Empty;
    }

    public static IReadOnlyList<OverworldLandmark> CollectExploredLandmarks(Overworld overworld, RunScenario? scenario)
    {
        if (overworld.IslandPlan is null)
        {
            return [];
        }

        IslandPlan plan = overworld.IslandPlan;
        var landmarks = new List<OverworldLandmark>();
        var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (StructurePlacement structure in plan.Structures)
        {
            if (!IsFootprintExplored(overworld, structure.GlobalOriginX, structure.GlobalOriginY, structure.Width, structure.Height))
            {
                continue;
            }

            string baseName = GetStructureName(structure.Type);
            AddLandmark(
                landmarks,
                nameCounts,
                structure.GlobalOriginX,
                structure.GlobalOriginY,
                structure.Width,
                structure.Height,
                baseName,
                OverworldLandmarkKind.Structure,
                ResolveObjectiveKind(scenario, structure.GlobalOriginX, structure.GlobalOriginY, structure.Width, structure.Height));
        }

        foreach (RuinSite ruin in plan.RuinSites)
        {
            if (!IsFootprintExplored(overworld, ruin.GlobalOriginX, ruin.GlobalOriginY, ruin.Width, ruin.Height))
            {
                continue;
            }

            string baseName = ruin.Kind == RuinKind.WarFortification ? "Fortification" : "Ruins";
            AddLandmark(
                landmarks,
                nameCounts,
                ruin.GlobalOriginX,
                ruin.GlobalOriginY,
                ruin.Width,
                ruin.Height,
                baseName,
                OverworldLandmarkKind.Ruin,
                ResolveObjectiveKind(scenario, ruin.GlobalOriginX, ruin.GlobalOriginY, ruin.Width, ruin.Height));
        }

        foreach (VolcanicSite site in plan.VolcanicSites)
        {
            var coord = new WorldCoord(site.X, site.Y);
            if (!overworld.Explored[overworld.GetIndex(coord)])
            {
                continue;
            }

            string baseName = site.Origin switch
            {
                VolcanicOrigin.MantlePlume => "Hotspot vent",
                VolcanicOrigin.SubductionArc => "Volcanic arc",
                VolcanicOrigin.RiftVolcano => "Rift volcano",
                VolcanicOrigin.CollisionVolcanism => "Collision peak",
                _ => "Volcanic vent"
            };

            int globalX = site.X * LocalMap.Width + LocalMap.Width / 2;
            int globalY = site.Y * LocalMap.Height + LocalMap.Height / 2;
            AddLandmark(
                landmarks,
                nameCounts,
                globalX,
                globalY,
                1,
                1,
                baseName,
                OverworldLandmarkKind.Volcanic,
                ResolveObjectiveKind(scenario, globalX, globalY, 1, 1));
        }

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                var coord = new WorldCoord(x, y);
                if (!overworld.Explored[overworld.GetIndex(coord)])
                {
                    continue;
                }

                ref IslandCellData cell = ref plan.GetCell(x, y);
                foreach ((IslandCellRole role, string name) in SiteRoles)
                {
                    if (!cell.Role.HasFlag(role))
                    {
                        continue;
                    }

                    int globalX = x * LocalMap.Width + LocalMap.Width / 2;
                    int globalY = y * LocalMap.Height + LocalMap.Height / 2;
                    AddLandmark(
                        landmarks,
                        nameCounts,
                        globalX,
                        globalY,
                        1,
                        1,
                        name,
                        OverworldLandmarkKind.Site,
                        ResolveObjectiveKind(scenario, globalX, globalY, 1, 1));
                    break;
                }
            }
        }

        return landmarks;
    }

    public static bool TryResolveEntryPoint(
        IslandPlan plan,
        WorldCoord worldCell,
        LocalMap map,
        out LocalCoord entryPoint)
    {
        entryPoint = default;

        StructurePlacement? structure = FindStructureAt(plan, worldCell);
        if (structure is not null)
        {
            var blueprint = StructureBlueprintCatalogDefaults.Create().ResolveById(structure.BlueprintId);
            LocalCoord door = StructurePlacementQueries.ToLocalCoord(
                worldCell,
                structure,
                blueprint.DoorX,
                blueprint.DoorY);

            if (structure.Type == StructureType.Helipad)
            {
                door = StructurePlacementQueries.ToLocalCoord(
                    worldCell,
                    structure,
                    blueprint.DoorX,
                    blueprint.DoorY);
            }

            if (map.Contains(door))
            {
                entryPoint = WalkabilityHelper.FindNearestWalkable(map, door);
                return true;
            }
        }

        RuinSite? ruin = FindRuinAt(plan, worldCell);
        if (ruin is not null)
        {
            if (TryResolveRuinEntry(ruin, worldCell, map, out entryPoint))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddLandmark(
        List<OverworldLandmark> landmarks,
        Dictionary<string, int> nameCounts,
        int globalOriginX,
        int globalOriginY,
        int width,
        int height,
        string baseName,
        OverworldLandmarkKind kind,
        OverworldLandmarkObjectiveKind objectiveKind)
    {
        int count = nameCounts.GetValueOrDefault(baseName) + 1;
        nameCounts[baseName] = count;
        string displayName = count > 1 ? $"{baseName} {count}" : baseName;

        landmarks.Add(new OverworldLandmark(
            globalOriginX,
            globalOriginY,
            width,
            height,
            displayName,
            objectiveKind,
            kind));
    }

    private static bool IsFootprintExplored(Overworld overworld, int globalOriginX, int globalOriginY, int width, int height)
    {
        int minCellX = globalOriginX / LocalMap.Width;
        int minCellY = globalOriginY / LocalMap.Height;
        int maxCellX = (globalOriginX + width - 1) / LocalMap.Width;
        int maxCellY = (globalOriginY + height - 1) / LocalMap.Height;

        for (int cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                var coord = new WorldCoord(cellX, cellY);
                if (overworld.Contains(coord) && overworld.Explored[overworld.GetIndex(coord)])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetStructureName(StructureType type) => type switch
    {
        StructureType.VisitorCenter => "Visitor Center",
        StructureType.Dock => "Dock",
        StructureType.Helipad => "Helipad",
        StructureType.Hotel => "Hotel",
        StructureType.Restaurant => "Restaurant",
        StructureType.Attraction => "Attraction",
        StructureType.MaintenanceCompound => "Maintenance compound",
        _ => "Structure"
    };

    private static OverworldLandmarkObjectiveKind ResolveObjectiveKind(
        RunScenario? scenario,
        int globalOriginX,
        int globalOriginY,
        int width,
        int height)
    {
        if (scenario is null)
        {
            return OverworldLandmarkObjectiveKind.None;
        }

        int centerX = (globalOriginX + width / 2) / LocalMap.Width;
        int centerY = (globalOriginY + height / 2) / LocalMap.Height;
        var coord = new WorldCoord(centerX, centerY);

        if (scenario.EscapeTarget == coord)
        {
            return OverworldLandmarkObjectiveKind.Escape;
        }

        if (scenario.MysteryTarget == coord)
        {
            return OverworldLandmarkObjectiveKind.Mystery;
        }

        return OverworldLandmarkObjectiveKind.None;
    }

    private static StructurePlacement? FindStructureAt(IslandPlan plan, WorldCoord worldCell)
    {
        foreach (StructurePlacement structure in plan.Structures)
        {
            if (CoordinateMath.OverlapsCell(
                    structure.GlobalOriginX,
                    structure.GlobalOriginY,
                    structure.Width,
                    structure.Height,
                    worldCell))
            {
                return structure;
            }
        }

        return null;
    }

    private static RuinSite? FindRuinAt(IslandPlan plan, WorldCoord worldCell)
    {
        foreach (RuinSite ruin in plan.RuinSites)
        {
            if (CoordinateMath.OverlapsCell(
                    ruin.GlobalOriginX,
                    ruin.GlobalOriginY,
                    ruin.Width,
                    ruin.Height,
                    worldCell))
            {
                return ruin;
            }
        }

        return null;
    }

    private static bool TryResolveRuinEntry(
        RuinSite ruin,
        WorldCoord worldCell,
        LocalMap map,
        out LocalCoord entryPoint)
    {
        int entryGlobalX = ruin.GlobalOriginX + ruin.Width / 2;
        int entryGlobalY = ruin.GlobalOriginY + ruin.Height - 1;
        return TryGlobalToLocal(worldCell, map, entryGlobalX, entryGlobalY, out entryPoint);
    }

    private static bool TryGlobalToLocal(
        WorldCoord worldCell,
        LocalMap map,
        int globalX,
        int globalY,
        out LocalCoord entryPoint)
    {
        int localX = globalX - worldCell.X * LocalMap.Width;
        int localY = globalY - worldCell.Y * LocalMap.Height;
        var coord = new LocalCoord(localX, localY);

        if (!map.Contains(coord))
        {
            entryPoint = default;
            return false;
        }

        entryPoint = WalkabilityHelper.FindNearestWalkable(map, coord);
        return true;
    }
}

public readonly record struct OverworldLandmark(
    int GlobalOriginX,
    int GlobalOriginY,
    int Width,
    int Height,
    string Name,
    OverworldLandmarkObjectiveKind ObjectiveKind,
    OverworldLandmarkKind Kind)
{
    public int X => (GlobalOriginX + Width / 2) / LocalMap.Width;
    public int Y => (GlobalOriginY + Height / 2) / LocalMap.Height;
}
