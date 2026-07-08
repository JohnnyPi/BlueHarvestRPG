using Game.Simulation.World;

namespace Game.Simulation.World.Island;

public sealed class IslandGenerationSnapshot
{
    public IslandGenerationCheckpoint Checkpoint { get; init; }

    public bool[] IsLand { get; init; } = [];

    public BiomeId[] Biomes { get; init; } = [];

    public float[] Elevations { get; init; } = [];

    public static IslandGenerationSnapshot Capture(IslandPlan plan, IslandGenerationCheckpoint checkpoint)
    {
        int count = plan.Width * plan.Height;
        var isLand = new bool[count];
        var biomes = new BiomeId[count];
        var elevations = new float[count];

        for (int i = 0; i < count; i++)
        {
            ref IslandCellData cell = ref plan.Cells[i];
            isLand[i] = cell.IsLand;
            biomes[i] = cell.Biome;
            elevations[i] = cell.Elevation;
        }

        return new IslandGenerationSnapshot
        {
            Checkpoint = checkpoint,
            IsLand = isLand,
            Biomes = biomes,
            Elevations = elevations,
        };
    }
}
