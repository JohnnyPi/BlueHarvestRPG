using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Generation.Regional;
using Game.Simulation.Coordinates;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class LavaFlowStage
{
    private const uint StageSalt = 25;
    private static readonly (int Dx, int Dy)[] Neighbors = [(1, 0), (0, 1), (-1, 0), (0, -1)];

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        plan.LavaFlowGraph.Clear();
        plan.LavaFlowGraph.RoadTraversalPenalty = Math.Max(0f, config.LavaFlowRoadTraversalPenalty);
        if (config.LavaFlowCount <= 0 || config.LavaFlowMaxLength <= 1)
        {
            return;
        }

        var random = new DeterministicRandom(SeedUtility.DeriveStage(seed, StageSalt));
        foreach (VolcanicSite site in plan.VolcanicSites)
        {
            for (int flowIndex = 0; flowIndex < config.LavaFlowCount; flowIndex++)
            {
                float angle = MathF.Tau * (flowIndex + random.NextFloat() * 0.65f) / config.LavaFlowCount;
                List<WorldCoord> path = TraceFlow(plan, site, config, angle, flowIndex);
                if (path.Count < 2)
                {
                    continue;
                }

                var flow = new LavaFlow();
                flow.Path.AddRange(path);
                plan.LavaFlowGraph.Flows.Add(flow);
                plan.LavaFlowGraph.AddPath(path);
                GlobalTilePathUtility.AddPathWithBorderRuns(
                    plan.LavaFlowGraph.GlobalLavaTiles,
                    path,
                    config.LavaFlowWidth);
            }
        }
    }

    private static List<WorldCoord> TraceFlow(
        IslandPlan plan,
        VolcanicSite site,
        IslandDefinition config,
        float preferredAngle,
        int flowIndex)
    {
        var current = new WorldCoord(site.X, site.Y);
        var path = new List<WorldCoord> { current };
        var visited = new HashSet<WorldCoord> { current };
        float preferredX = MathF.Cos(preferredAngle);
        float preferredY = MathF.Sin(preferredAngle);

        for (int step = 1; step < config.LavaFlowMaxLength; step++)
        {
            WorldCoord? best = null;
            float bestScore = float.MaxValue;
            float currentElevation = plan.GetCell(current).Elevation;

            foreach ((int dx, int dy) in Neighbors)
            {
                var candidate = new WorldCoord(current.X + dx, current.Y + dy);
                if (!plan.Contains(candidate.X, candidate.Y)
                    || !plan.IsLand(candidate.X, candidate.Y)
                    || visited.Contains(candidate))
                {
                    continue;
                }

                float elevation = plan.GetCell(candidate).Elevation;
                float uphill = MathF.Max(0f, elevation - currentElevation) * 80f;
                float directionPenalty = (1f - (dx * preferredX + dy * preferredY)) * 0.22f;
                float radialProgress = VolcanicConeUtility.ComputeNormalizedDistance(site, candidate.X, candidate.Y);
                float noise = NoiseUtility.Fbm(
                    plan.Seed + (ulong)(flowIndex * 97 + 31),
                    candidate.X * 0.19f,
                    candidate.Y * 0.19f,
                    octaves: 2) * config.LavaFlowMeanderStrength;
                float score = elevation + uphill + directionPenalty - radialProgress * 0.08f + noise;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best is null)
            {
                break;
            }

            current = best.Value;
            visited.Add(current);
            path.Add(current);
            if (VolcanicConeUtility.ComputeNormalizedDistance(site, current.X, current.Y)
                >= config.LavaFlowTerminationRadius)
            {
                break;
            }
        }

        return path;
    }
}
