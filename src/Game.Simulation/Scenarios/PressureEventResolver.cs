using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;
using Game.Simulation.World;

namespace Game.Simulation.Scenarios;

public static class PressureEventResolver
{
    public const int EvacWindowHours = 36;

    public static void Apply(GameSession session, int threshold)
    {
        switch (threshold)
        {
            case 1:
                session.FinaleThreats.Record(FinaleThreatId.RaptorPack);
                ApplyPredatorStir(session);
                break;
            case 2:
                session.FinaleThreats.Record(FinaleThreatId.StormFront);
                session.PressureState.TravelStaminaPenalty += 10;
                break;
            case 3:
                session.FinaleThreats.Record(FinaleThreatId.PowerFailure);
                session.PressureState.TravelStaminaPenalty += 10;
                session.PressureState.HazardousTravelCell = PickHazardousTravelCell(session);
                break;
            case 4:
                session.PressureState.EvacHoursRemaining ??= EvacWindowHours;
                break;
            case >= 5:
                if (session.PressureState.EvacHoursRemaining is int remaining)
                {
                    session.PressureState.EvacHoursRemaining = Math.Min(remaining, 12);
                }
                else
                {
                    session.PressureState.EvacHoursRemaining = 12;
                }

                session.PressureState.TravelStaminaPenalty += 5;
                break;
        }
    }

    public static string DescribeBlockedTravel(GameSession session, WorldCoord target)
    {
        if (session.PressureState.HazardousTravelCell == target)
        {
            return "Downed power lines block that route.";
        }

        return string.Empty;
    }

    private static void ApplyPredatorStir(GameSession session)
    {
        if (session.ViewMode == GameViewMode.LocalMap && session.ActiveLocalMap is not null)
        {
            if (EntityFactory.TrySpawnPressurePredator(session))
            {
                session.MessageLog.Add("Something large crashes through the brush nearby!");
            }

            return;
        }

        session.PressureState.PendingPredatorSpawn = true;
    }

    private static WorldCoord? PickHazardousTravelCell(GameSession session)
    {
        if (session.Overworld.IslandPlan is null)
        {
            return null;
        }

        var candidates = new List<WorldCoord>();
        WorldCoord player = session.PlayerWorldPosition;
        RunScenario? scenario = session.RunScenario;

        for (int dy = -10; dy <= 10; dy++)
        {
            for (int dx = -10; dx <= 10; dx++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var coord = new WorldCoord(player.X + dx, player.Y + dy);
                if (!session.Overworld.Contains(coord))
                {
                    continue;
                }

                if (!session.Overworld.Explored[session.Overworld.GetIndex(coord)])
                {
                    continue;
                }

                if (!BiomeTraversal.IsPassable(session.Overworld.GetCellValue(coord).Biome))
                {
                    continue;
                }

                if (scenario?.EscapeTarget == coord || scenario?.MysteryTarget == coord)
                {
                    continue;
                }

                candidates.Add(coord);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        candidates.Sort(static (a, b) =>
        {
            int cmp = a.Y.CompareTo(b.Y);
            return cmp != 0 ? cmp : a.X.CompareTo(b.X);
        });

        ulong pick = session.Overworld.Seed ^ (ulong)session.PressureClock.Pressure;
        int index = (int)(pick % (ulong)candidates.Count);
        return candidates[index];
    }
}
