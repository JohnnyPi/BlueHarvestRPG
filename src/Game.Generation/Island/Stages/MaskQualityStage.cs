using Game.Content.Definitions;
using Game.Generation.Island;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public sealed class MaskQualityResult
{
    public bool Passed { get; init; }
    public int LandViolations { get; init; }
    public int CoastViolations { get; init; }
    public int MaxAxisAlignedCoastRun { get; init; }
}

public static class MaskQualityStage
{
    private const uint StageSalt = 22;

    public static MaskQualityResult ValidateCropWindow(
        IslandPlan overscanPlan,
        IslandDefinition config,
        int cropWidth,
        int cropHeight)
    {
        OceanFrameDefinition frame = config.OceanFrame;
        int offsetX = (overscanPlan.Width - cropWidth) / 2;
        int offsetY = (overscanPlan.Height - cropHeight) / 2;
        float landThreshold = config.IslandShape.LandThreshold;

        int landViolations = 0;
        int coastViolations = 0;

        for (int y = 0; y < cropHeight; y++)
        {
            for (int x = 0; x < cropWidth; x++)
            {
                int sourceX = x + offsetX;
                int sourceY = y + offsetY;
                int edgeDist = Math.Min(
                    Math.Min(x, y),
                    Math.Min(cropWidth - 1 - x, cropHeight - 1 - y));

                int index = sourceY * overscanPlan.Width + sourceX;
                bool isLand = overscanPlan.IslandMask.Length > index
                    && overscanPlan.IslandMask[index] > landThreshold;

                if (isLand && edgeDist < frame.MinLandDistanceFromEdge)
                {
                    landViolations++;
                }

                bool isCoast = IsCoastCell(overscanPlan, sourceX, sourceY, landThreshold);
                if (isCoast && edgeDist < frame.MinCoastDistanceFromEdge)
                {
                    coastViolations++;
                }
            }
        }

        int maxRun = ComputeMaxAxisAlignedCoastRunInCrop(
            overscanPlan,
            offsetX,
            offsetY,
            cropWidth,
            cropHeight,
            landThreshold,
            frame.EdgeLinearityBand);

        bool passed = landViolations == 0
            && coastViolations == 0
            && maxRun <= frame.MaxAxisAlignedCoastRun;

        return new MaskQualityResult
        {
            Passed = passed,
            LandViolations = landViolations,
            CoastViolations = coastViolations,
            MaxAxisAlignedCoastRun = maxRun,
        };
    }

    public static bool TryGenerateValidMask(
        IslandPlan overscanPlan,
        IslandDefinition config,
        int cropWidth,
        int cropHeight,
        ulong seed,
        out MaskQualityResult result,
        IslandGenerationProgressReporter? progress = null)
    {
        OceanFrameDefinition frame = config.OceanFrame;
        int attempts = Math.Max(1, frame.MaxRegenerationAttempts);
        float shapeScale = OverscanShapeFitting.ComputeInitialShapeScale(
            overscanPlan.Width,
            overscanPlan.Height,
            cropWidth,
            cropHeight,
            config);
        result = new MaskQualityResult();

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            string attemptPrefix = attempts > 1 ? $"Mask {attempt + 1}/{attempts}: " : string.Empty;
            ulong attemptSeed = SeedUtility.DeriveStage(seed, StageSalt + (uint)attempt);
            RunSubStage(progress, $"{attemptPrefix}island mask", () =>
                IslandMaskStage.Execute(
                    overscanPlan,
                    config,
                    attemptSeed,
                    overscanGeneration: true,
                    shapeScale));
            RunSubStage(progress, $"{attemptPrefix}coast distance", () =>
                CoastDistanceStage.Execute(overscanPlan, config));
            RunSubStage(progress, $"{attemptPrefix}coastline cleanup", () =>
                CoastlineCleanupStage.Execute(overscanPlan, config));
            RunSubStage(progress, $"{attemptPrefix}coastline variation", () =>
                CoastlineVariationStage.Execute(overscanPlan, config, attemptSeed));

            result = RunSubStage(progress, $"{attemptPrefix}validation", () =>
                ValidateCropWindow(overscanPlan, config, cropWidth, cropHeight));
            if (result.Passed)
            {
                return true;
            }

            shapeScale *= 0.94f;
        }

        while (!result.Passed && shapeScale > 0.45f && attempts < frame.MaxRegenerationAttempts * 3)
        {
            shapeScale *= 0.94f;
            ulong attemptSeed = SeedUtility.DeriveStage(seed, StageSalt + (uint)attempts);
            attempts++;

            IslandMaskStage.Execute(
                overscanPlan,
                config,
                attemptSeed,
                overscanGeneration: true,
                shapeScale);
            CoastDistanceStage.Execute(overscanPlan, config);
            CoastlineCleanupStage.Execute(overscanPlan, config);
            CoastlineVariationStage.Execute(overscanPlan, config, attemptSeed);
            result = ValidateCropWindow(overscanPlan, config, cropWidth, cropHeight);
        }

        return result.Passed;
    }

    private static void RunSubStage(IslandGenerationProgressReporter? progress, string name, Action action)
    {
        if (progress is null)
        {
            action();
            return;
        }

        progress.RunStage(name, action);
    }

    private static T RunSubStage<T>(IslandGenerationProgressReporter? progress, string name, Func<T> action)
    {
        if (progress is null)
        {
            return action();
        }

        T result = default!;
        progress.RunStage(name, () => result = action());
        return result;
    }

    private static bool IsCoastCell(IslandPlan plan, int x, int y, float landThreshold)
    {
        int index = y * plan.Width + x;
        if (plan.IslandMask.Length <= index || plan.IslandMask[index] <= landThreshold)
        {
            return false;
        }

        (int Dx, int Dy)[] neighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        foreach ((int dx, int dy) in neighbors)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (!plan.Contains(nx, ny))
            {
                return true;
            }

            int neighborIndex = ny * plan.Width + nx;
            if (plan.IslandMask[neighborIndex] <= landThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private static int ComputeMaxAxisAlignedCoastRunInCrop(
        IslandPlan plan,
        int offsetX,
        int offsetY,
        int cropWidth,
        int cropHeight,
        float landThreshold,
        int edgeBand)
    {
        int maxRun = 0;

        for (int y = 0; y < cropHeight; y++)
        {
            int run = 0;
            for (int x = 0; x < cropWidth; x++)
            {
                int edgeDist = Math.Min(Math.Min(x, y), Math.Min(cropWidth - 1 - x, cropHeight - 1 - y));
                bool isCoast = edgeDist <= edgeBand
                    && IsCoastCell(plan, x + offsetX, y + offsetY, landThreshold);

                if (isCoast)
                {
                    run++;
                    maxRun = Math.Max(maxRun, run);
                }
                else
                {
                    run = 0;
                }
            }
        }

        for (int x = 0; x < cropWidth; x++)
        {
            int run = 0;
            for (int y = 0; y < cropHeight; y++)
            {
                int edgeDist = Math.Min(Math.Min(x, y), Math.Min(cropWidth - 1 - x, cropHeight - 1 - y));
                bool isCoast = edgeDist <= edgeBand
                    && IsCoastCell(plan, x + offsetX, y + offsetY, landThreshold);

                if (isCoast)
                {
                    run++;
                    maxRun = Math.Max(maxRun, run);
                }
                else
                {
                    run = 0;
                }
            }
        }

        return maxRun;
    }
}
