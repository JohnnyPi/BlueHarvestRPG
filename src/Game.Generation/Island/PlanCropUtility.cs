using Game.Simulation.World.Island;

namespace Game.Generation.Island;

public static class PlanCropUtility
{
    public static IslandPlan CropCenter(IslandPlan source, int cropWidth, int cropHeight)
    {
        int offsetX = (source.Width - cropWidth) / 2;
        int offsetY = (source.Height - cropHeight) / 2;
        return CropAt(source, cropWidth, cropHeight, offsetX, offsetY);
    }

    public static IslandPlan CropCenteredOnLandmass(
        IslandPlan source,
        int cropWidth,
        int cropHeight,
        float landThreshold)
    {
        (int offsetX, int offsetY) = ComputeLandmassCentroidCropOffset(
            source,
            cropWidth,
            cropHeight,
            landThreshold);
        return CropAt(source, cropWidth, cropHeight, offsetX, offsetY);
    }

    public static (int OffsetX, int OffsetY) ComputeLandmassCentroidCropOffset(
        IslandPlan source,
        int cropWidth,
        int cropHeight,
        float landThreshold)
    {
        int geometricOffsetX = (source.Width - cropWidth) / 2;
        int geometricOffsetY = (source.Height - cropHeight) / 2;

        if (source.IslandMask.Length != source.Width * source.Height)
        {
            return (geometricOffsetX, geometricOffsetY);
        }

        double sumX = 0;
        double sumY = 0;
        double sumWeight = 0;

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                float mask = source.IslandMask[y * source.Width + x];
                if (mask <= landThreshold)
                {
                    continue;
                }

                sumX += x * mask;
                sumY += y * mask;
                sumWeight += mask;
            }
        }

        if (sumWeight <= 0)
        {
            return (geometricOffsetX, geometricOffsetY);
        }

        float centroidX = (float)(sumX / sumWeight);
        float centroidY = (float)(sumY / sumWeight);

        int offsetX = (int)MathF.Round(centroidX - (cropWidth - 1) * 0.5f);
        int offsetY = (int)MathF.Round(centroidY - (cropHeight - 1) * 0.5f);

        int maxOffsetX = Math.Max(0, source.Width - cropWidth);
        int maxOffsetY = Math.Max(0, source.Height - cropHeight);
        offsetX = Math.Clamp(offsetX, 0, maxOffsetX);
        offsetY = Math.Clamp(offsetY, 0, maxOffsetY);

        return (offsetX, offsetY);
    }

    public static IslandPlan CropAt(
        IslandPlan source,
        int cropWidth,
        int cropHeight,
        int offsetX,
        int offsetY)
    {
        var cropped = new IslandPlan(cropWidth, cropHeight, source.Seed)
        {
            GenerationSnapshots = source.GenerationSnapshots,
            GenerationDiagnostics = source.GenerationDiagnostics,
        };

        cropped.IslandMask = CropField(source.IslandMask, source.Width, source.Height, offsetX, offsetY, cropWidth, cropHeight);
        cropped.CoastDistance = CropField(source.CoastDistance, source.Width, source.Height, offsetX, offsetY, cropWidth, cropHeight);
        cropped.Concavity = CropField(source.Concavity, source.Width, source.Height, offsetX, offsetY, cropWidth, cropHeight);
        cropped.CoastalWidthVariation = CropField(
            source.CoastalWidthVariation,
            source.Width,
            source.Height,
            offsetX,
            offsetY,
            cropWidth,
            cropHeight);
        cropped.BeachWidth = CropField(source.BeachWidth, source.Width, source.Height, offsetX, offsetY, cropWidth, cropHeight);
        cropped.ShallowWaterWidth = CropField(
            source.ShallowWaterWidth,
            source.Width,
            source.Height,
            offsetX,
            offsetY,
            cropWidth,
            cropHeight);

        return cropped;
    }

    private static float[] CropField(
        float[] sourceField,
        int sourceWidth,
        int sourceHeight,
        int offsetX,
        int offsetY,
        int cropWidth,
        int cropHeight)
    {
        if (sourceField.Length != sourceWidth * sourceHeight)
        {
            return [];
        }

        var target = new float[cropWidth * cropHeight];
        for (int y = 0; y < cropHeight; y++)
        {
            for (int x = 0; x < cropWidth; x++)
            {
                int sourceX = x + offsetX;
                int sourceY = y + offsetY;
                target[y * cropWidth + x] = sourceField[sourceY * sourceWidth + sourceX];
            }
        }

        return target;
    }
}
