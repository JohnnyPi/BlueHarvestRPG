using Game.Content.Definitions;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class CoastalLandformStage
{
    public static void Execute(IslandPlan plan, IslandDefinition config)
    {
        int count = plan.Width * plan.Height;
        if (plan.CoastalLandforms.Length != count)
        {
            plan.CoastalLandforms = new CoastalLandform[count];
        }

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int index = y * plan.Width + x;
                ref IslandCellData cell = ref plan.GetCell(x, y);
                plan.CoastalLandforms[index] = Classify(plan, x, y, index, cell, config);
            }
        }
    }

    private static CoastalLandform Classify(
        IslandPlan plan,
        int x,
        int y,
        int index,
        IslandCellData cell,
        IslandDefinition config)
    {
        float coastDistance = plan.CoastDistance.Length > index ? plan.CoastDistance[index] : 0f;
        float concavity = plan.Concavity.Length > index ? plan.Concavity[index] : 0f;
        float slope = plan.Slope.Length > index ? plan.Slope[index] : 0f;
        float waveExposure = plan.WaveExposure.Length > index ? plan.WaveExposure[index] : 0f;
        float riverInfluence = plan.RiverInfluence.Length > index ? plan.RiverInfluence[index] : 0f;
        float drainage = plan.Drainage.Length > index ? plan.Drainage[index] : 0f;

        if (!cell.IsLand)
        {
            if (coastDistance < 0f && coastDistance > -config.ShelfWidth && concavity > 0.15f)
            {
                return CoastalLandform.ReefShallows;
            }

            return CoastalLandform.None;
        }

        bool nearCoast = cell.IsCoast || coastDistance <= config.InlandCoastDistance;
        if (!nearCoast)
        {
            return CoastalLandform.None;
        }

        if (riverInfluence > 0.55f && cell.Elevation < config.LandElevationThreshold + 0.08f)
        {
            return cell.IsCoast ? CoastalLandform.Estuary : CoastalLandform.RiverMouth;
        }

        if (slope > 0.45f && waveExposure > 0.5f)
        {
            return CoastalLandform.Cliff;
        }

        if (slope > 0.3f)
        {
            return CoastalLandform.RockyCoast;
        }

        if (concavity > 0.2f && cell.Moisture > 0.55f)
        {
            return CoastalLandform.Mangrove;
        }

        if (concavity > 0.15f && waveExposure < 0.35f)
        {
            return CoastalLandform.LagoonEdge;
        }

        if (concavity > 0.12f)
        {
            return CoastalLandform.Bay;
        }

        if (concavity < -0.1f && waveExposure > 0.45f)
        {
            return CoastalLandform.Headland;
        }

        if (cell.VolcanicActivity > 0.2f)
        {
            return CoastalLandform.RockyCoast;
        }

        if (drainage > 0.4f)
        {
            return CoastalLandform.Estuary;
        }

        return CoastalLandform.SandyBeach;
    }
}
