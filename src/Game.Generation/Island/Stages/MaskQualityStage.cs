using Game.Content.Definitions;
using Game.Generation.Island;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public sealed class MaskQualityResult
{
    public bool Passed { get; init; }
    public int LandViolations { get; init; }
    public int CoastViolations { get; init; }
    public int MaxAxisAlignedCoastRun { get; init; }
    public float LandCoverageRatio { get; init; }
    public float ShapeScale { get; init; }
    public ulong MaskSeed { get; init; }
    public int CropOffsetX { get; init; }
    public int CropOffsetY { get; init; }
    public IReadOnlyList<float> AttemptedScales { get; init; } = [];
}

public static class MaskQualityStage
{
    private const uint StageSalt = 22;

    public static MaskQualityResult ValidateCropWindow(
        IslandPlan overscanPlan,
        IslandDefinition config,
        int cropWidth,
        int cropHeight,
        int? offsetX = null,
        int? offsetY = null)
    {
        OceanFrameDefinition frame = config.OceanFrame;
        float landThreshold = config.IslandShape.LandThreshold;

        (int resolvedOffsetX, int resolvedOffsetY) = offsetX is int ox && offsetY is int oy
            ? (ox, oy)
            : PlanCropUtility.ComputeLandmassCentroidCropOffset(
                overscanPlan,
                cropWidth,
                cropHeight,
                landThreshold);

        int landViolations = 0;
        int coastViolations = 0;

        for (int y = 0; y < cropHeight; y++)
        {
            for (int x = 0; x < cropWidth; x++)
            {
                int sourceX = x + resolvedOffsetX;
                int sourceY = y + resolvedOffsetY;
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
            resolvedOffsetX,
            resolvedOffsetY,
            cropWidth,
            cropHeight,
            landThreshold,
            frame.EdgeLinearityBand);

        bool passed = landViolations == 0
            && coastViolations == 0
            && maxRun <= frame.MaxAxisAlignedCoastRun;
        int landCount = 0;
        for (int y = 0; y < cropHeight; y++)
        {
            for (int x = 0; x < cropWidth; x++)
            {
                int index = (y + resolvedOffsetY) * overscanPlan.Width + x + resolvedOffsetX;
                if (overscanPlan.IslandMask[index] > landThreshold)
                {
                    landCount++;
                }
            }
        }

        return new MaskQualityResult
        {
            Passed = passed,
            LandViolations = landViolations,
            CoastViolations = coastViolations,
            MaxAxisAlignedCoastRun = maxRun,
            LandCoverageRatio = landCount / (float)(cropWidth * cropHeight),
            CropOffsetX = resolvedOffsetX,
            CropOffsetY = resolvedOffsetY,
        };
    }

    public static bool TryGenerateValidMask(
        IslandPlan overscanPlan,
        IslandDefinition config,
        int cropWidth,
        int cropHeight,
        ulong seed,
        out MaskQualityResult result,
        out int cropOffsetX,
        out int cropOffsetY,
        IslandGenerationProgressReporter? progress = null)
    {
        OceanFrameDefinition frame = config.OceanFrame;
        int evaluationBudget = Math.Max(4, frame.MaxRegenerationAttempts);
        int candidateCount = Math.Clamp(evaluationBudget / 8, 1, 3);
        int evaluationsPerCandidate = Math.Clamp(evaluationBudget / candidateCount, 4, 6);
        cropOffsetX = (overscanPlan.Width - cropWidth) / 2;
        cropOffsetY = (overscanPlan.Height - cropHeight) / 2;
        var attemptedScales = new List<float>();
        var validCandidates = new List<MaskQualityResult>();
        var evaluatedCandidates = new List<MaskQualityResult>();

        MaskQualityResult RunAttempt(ulong attemptSeed, float scale)
        {
            IslandMaskStage.Execute(overscanPlan, config, attemptSeed, overscanGeneration: true, scale);
            CoastlineCleanupStage.Execute(overscanPlan, config, recomputeCoastDistance: false);
            CoastlineVariationStage.Execute(overscanPlan, config, attemptSeed);
            attemptedScales.Add(scale);
            MaskQualityResult measured = ValidateCropWindow(overscanPlan, config, cropWidth, cropHeight);
            var candidate = new MaskQualityResult
            {
                Passed = measured.Passed,
                LandViolations = measured.LandViolations,
                CoastViolations = measured.CoastViolations,
                MaxAxisAlignedCoastRun = measured.MaxAxisAlignedCoastRun,
                LandCoverageRatio = measured.LandCoverageRatio,
                ShapeScale = scale,
                MaskSeed = attemptSeed,
                CropOffsetX = measured.CropOffsetX,
                CropOffsetY = measured.CropOffsetY,
            };
            evaluatedCandidates.Add(candidate);
            return candidate;
        }

        for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
        {
            ulong candidateSeed = SeedUtility.DeriveStage(seed, StageSalt + (uint)candidateIndex);
            string prefix = candidateCount > 1 ? $"Mask {candidateIndex + 1}/{candidateCount}: " : string.Empty;
            MaskQualityResult fullScale = RunSubStage(
                progress,
                $"{prefix}fit scale 1.000",
                () => RunAttempt(candidateSeed, 1f));
            if (fullScale.Passed)
            {
                validCandidates.Add(fullScale);
                continue;
            }

            float estimatedScale = OverscanShapeFitting.ComputeInitialShapeScale(
                overscanPlan.Width,
                overscanPlan.Height,
                cropWidth,
                cropHeight,
                config,
                fullScale.CropOffsetX,
                fullScale.CropOffsetY);
            float low = Math.Clamp(estimatedScale, 0.35f, 0.95f);
            MaskQualityResult lowResult = RunAttempt(candidateSeed, low);
            if (!lowResult.Passed && low > 0.35f)
            {
                low = 0.35f;
                lowResult = RunAttempt(candidateSeed, low);
            }

            if (!lowResult.Passed)
            {
                continue;
            }

            MaskQualityResult largestValid = lowResult;
            float high = 1f;
            for (int evaluation = 2; evaluation < evaluationsPerCandidate; evaluation++)
            {
                float middle = (low + high) * 0.5f;
                MaskQualityResult middleResult = RunAttempt(candidateSeed, middle);
                if (middleResult.Passed)
                {
                    low = middle;
                    largestValid = middleResult;
                }
                else
                {
                    high = middle;
                }
            }

            validCandidates.Add(largestValid);
        }

        if (validCandidates.Count == 0)
        {
            MaskQualityResult bestFailed = evaluatedCandidates
                .OrderBy(candidate => candidate.LandViolations + candidate.CoastViolations)
                .ThenBy(candidate => Math.Max(
                    0,
                    candidate.MaxAxisAlignedCoastRun - frame.MaxAxisAlignedCoastRun))
                .ThenByDescending(candidate => candidate.LandCoverageRatio)
                .ThenByDescending(candidate => candidate.ShapeScale)
                .First();
            MaskQualityResult failed = RunAttempt(bestFailed.MaskSeed, bestFailed.ShapeScale);
            result = WithAttempts(failed, attemptedScales);
            cropOffsetX = failed.CropOffsetX;
            cropOffsetY = failed.CropOffsetY;
            return false;
        }

        MaskQualityResult selected = validCandidates
            .OrderByDescending(candidate => candidate.LandCoverageRatio)
            .ThenBy(candidate => candidate.MaxAxisAlignedCoastRun)
            .ThenByDescending(candidate => candidate.ShapeScale)
            .ThenBy(candidate => candidate.MaskSeed)
            .First();
        MaskQualityResult final = RunAttempt(selected.MaskSeed, selected.ShapeScale);
        result = WithAttempts(final, attemptedScales);
        cropOffsetX = final.CropOffsetX;
        cropOffsetY = final.CropOffsetY;
        return true;
    }

    private static MaskQualityResult WithAttempts(
        MaskQualityResult source,
        IReadOnlyList<float> attemptedScales)
        => new()
        {
            Passed = source.Passed,
            LandViolations = source.LandViolations,
            CoastViolations = source.CoastViolations,
            MaxAxisAlignedCoastRun = source.MaxAxisAlignedCoastRun,
            LandCoverageRatio = source.LandCoverageRatio,
            ShapeScale = source.ShapeScale,
            MaskSeed = source.MaskSeed,
            CropOffsetX = source.CropOffsetX,
            CropOffsetY = source.CropOffsetY,
            AttemptedScales = attemptedScales.ToArray(),
        };

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
