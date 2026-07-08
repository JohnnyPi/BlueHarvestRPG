using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.World.Island;

namespace Game.Generation.Voronoi;

public readonly struct VoronoiSample
{
    public VoronoiSample(
        int nearestRegionId,
        float f1,
        float f2,
        float edge,
        ReadOnlySpan<int> blendRegionIds,
        ReadOnlySpan<float> blendWeights)
    {
        NearestRegionId = nearestRegionId;
        F1 = f1;
        F2 = f2;
        Edge = edge;
        BlendRegionIds = blendRegionIds.ToArray();
        BlendWeights = blendWeights.ToArray();
    }

    public int NearestRegionId { get; }
    public float F1 { get; }
    public float F2 { get; }
    public float Edge { get; }
    public int[] BlendRegionIds { get; }
    public float[] BlendWeights { get; }
}

public static class VoronoiField
{
    public static void ComputeField(
        IslandPlan plan,
        IslandDefinition config,
        ulong seed,
        int blendNeighborCount = 3)
    {
        int cellCount = plan.Width * plan.Height;
        plan.VoronoiF1 = new float[cellCount];
        plan.VoronoiF2 = new float[cellCount];
        plan.VoronoiEdge = new float[cellCount];
        plan.VoronoiBlendRegionIds = new int[cellCount * blendNeighborCount];
        plan.VoronoiBlendWeights = new float[cellCount * blendNeighborCount];

        float normScale = MathF.Sqrt(plan.Width * plan.Width + plan.Height * plan.Height);
        int neighborCount = Math.Clamp(blendNeighborCount, 1, 4);
        int nearestCapacity = Math.Min(plan.Regions.Count, 4);
        var nearestScratch = new (int RegionId, float DistSq)[nearestCapacity];

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                float nx = x / (float)Math.Max(1, plan.Width - 1);
                float ny = y / (float)Math.Max(1, plan.Height - 1);

                (float wx, float wy) = NoiseUtility.DomainWarp(
                    seed,
                    nx,
                    ny,
                    config.WarpLargeStrength,
                    config.WarpMediumStrength,
                    config.WarpSmallStrength);

                float queryX = x + (wx - nx) * plan.Width;
                float queryY = y + (wy - ny) * plan.Height;

                Array.Clear(nearestScratch);
                int found = 0;

                foreach (IslandRegion region in plan.Regions)
                {
                    float dx = queryX - region.SiteX;
                    float dy = queryY - region.SiteY;
                    float distSq = dx * dx + dy * dy;
                    InsertNearest(nearestScratch, ref found, region.Id, distSq);
                }

                if (found == 0)
                {
                    continue;
                }

                int index = y * plan.Width + x;
                float f1 = MathF.Sqrt(nearestScratch[0].DistSq) / normScale;
                float f2 = found > 1 ? MathF.Sqrt(nearestScratch[1].DistSq) / normScale : f1 + 0.001f;

                plan.RegionIds[index] = nearestScratch[0].RegionId;
                plan.VoronoiF1[index] = f1;
                plan.VoronoiF2[index] = f2;
                plan.VoronoiEdge[index] = MathF.Max(0f, f2 - f1);

                ref IslandCellData cell = ref plan.GetCell(x, y);
                cell.RegionId = nearestScratch[0].RegionId;

                float weightSum = 0f;
                int blendBase = index * neighborCount;
                for (int i = 0; i < neighborCount; i++)
                {
                    if (i >= found)
                    {
                        plan.VoronoiBlendRegionIds[blendBase + i] = nearestScratch[0].RegionId;
                        plan.VoronoiBlendWeights[blendBase + i] = 0f;
                        continue;
                    }

                    float dist = MathF.Sqrt(nearestScratch[i].DistSq) / normScale;
                    float weight = 1f / MathF.Pow(dist + 0.001f, config.BiomeBlendPower);
                    plan.VoronoiBlendRegionIds[blendBase + i] = nearestScratch[i].RegionId;
                    plan.VoronoiBlendWeights[blendBase + i] = weight;
                    weightSum += weight;
                }

                if (weightSum > 0f)
                {
                    for (int i = 0; i < neighborCount; i++)
                    {
                        plan.VoronoiBlendWeights[blendBase + i] /= weightSum;
                    }
                }
            }
        }
    }

    private static void InsertNearest(
        (int RegionId, float DistSq)[] nearest,
        ref int found,
        int regionId,
        float distSq)
    {
        int insertAt = found;
        for (int i = 0; i < found; i++)
        {
            if (distSq < nearest[i].DistSq)
            {
                insertAt = i;
                break;
            }
        }

        if (found < nearest.Length)
        {
            for (int i = found; i > insertAt; i--)
            {
                nearest[i] = nearest[i - 1];
            }

            nearest[insertAt] = (regionId, distSq);
            found++;
            return;
        }

        if (insertAt < nearest.Length)
        {
            for (int i = nearest.Length - 1; i > insertAt; i--)
            {
                nearest[i] = nearest[i - 1];
            }

            nearest[insertAt] = (regionId, distSq);
        }
    }
}
