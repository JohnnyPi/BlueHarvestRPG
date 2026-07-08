using Game.Simulation.World.Island;

namespace Game.Generation.Island;

public static class PlanCropUtility
{
    public static IslandPlan CropCenter(IslandPlan source, int cropWidth, int cropHeight)
    {
        int offsetX = (source.Width - cropWidth) / 2;
        int offsetY = (source.Height - cropHeight) / 2;

        var cropped = new IslandPlan(cropWidth, cropHeight, source.Seed)
        {
            GenerationSnapshots = source.GenerationSnapshots,
        };

        cropped.IslandMask = CropField(source.IslandMask, source.Width, source.Height, offsetX, offsetY, cropWidth, cropHeight);
        cropped.CoastDistance = CropField(source.CoastDistance, source.Width, source.Height, offsetX, offsetY, cropWidth, cropHeight);
        cropped.Concavity = CropField(source.Concavity, source.Width, source.Height, offsetX, offsetY, cropWidth, cropHeight);

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
