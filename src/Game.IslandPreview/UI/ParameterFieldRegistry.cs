using System.Reflection;
using System.Text;
using Game.Content.Definitions;

namespace Game.IslandPreview.UI;

public enum ParameterFieldKind
{
    Int,
    Float,
    Bool,
}

public enum ParameterFieldSource
{
    Island,
    BiomeRules,
}

public sealed class ParameterFieldDescriptor
{
    public required string Group { get; init; }
    public required string Name { get; init; }
    public required string Label { get; init; }
    public required ParameterFieldKind Kind { get; init; }
    public required ParameterFieldSource Source { get; init; }
    public required PropertyInfo Property { get; init; }
}

public static class ParameterFieldRegistry
{
    private static readonly (string Group, ParameterFieldSource Source, string[] Names)[] Groups =
    [
        ("World", ParameterFieldSource.Island,
        [
            nameof(IslandDefinition.OverworldSize),
            nameof(IslandDefinition.RegionCount),
            nameof(IslandDefinition.MinOceanBorderCells),
            nameof(IslandDefinition.UseLegacyIslandMask),
        ]),
        ("Island Shape", ParameterFieldSource.Island,
        [
            nameof(IslandDefinition.ShelfWidth),
            nameof(IslandDefinition.ShelfDepth),
            nameof(IslandDefinition.DeepOceanDepth),
            nameof(IslandDefinition.DeepOceanWidth),
            nameof(IslandDefinition.MinBeachCoastDistance),
            nameof(IslandDefinition.MaxBeachCoastDistance),
            nameof(IslandDefinition.MinShallowWaterCoastDistance),
            nameof(IslandDefinition.MaxShallowWaterCoastDistance),
            nameof(IslandDefinition.CoastalWidthVariationFrequency),
            nameof(IslandDefinition.CoastalWidthSmoothingPasses),
            nameof(IslandDefinition.InlandCoastDistance),
            nameof(IslandDefinition.LandCoastThreshold),
            nameof(IslandDefinition.CoastalRampStrength),
            nameof(IslandDefinition.VolcanicDomeStrength),
            nameof(IslandDefinition.DetailNoiseWeight),
            nameof(IslandDefinition.RidgeNoiseWeight),
            nameof(IslandDefinition.SeaLevel),
            nameof(IslandDefinition.SatelliteIslandCount),
            nameof(IslandDefinition.SatelliteMinRadius),
            nameof(IslandDefinition.SatelliteMaxRadius),
            nameof(IslandDefinition.MinLandComponentCells),
        ]),
        ("Legacy Island Mask", ParameterFieldSource.Island,
        [
            nameof(IslandDefinition.MainIslandRadius),
            nameof(IslandDefinition.MainIslandElongation),
            nameof(IslandDefinition.MainIslandRotation),
            nameof(IslandDefinition.MainIslandCenterOffsetX),
            nameof(IslandDefinition.MainIslandCenterOffsetY),
            nameof(IslandDefinition.MaskInnerRadius),
            nameof(IslandDefinition.MaskOuterRadius),
            nameof(IslandDefinition.MaskNoiseLarge),
            nameof(IslandDefinition.MaskNoiseMedium),
            nameof(IslandDefinition.MaskNoiseFine),
        ]),
        ("Height / Landmass", ParameterFieldSource.Island,
        [
            nameof(IslandDefinition.LandElevationThreshold),
            nameof(IslandDefinition.HeightMaskWeight),
            nameof(IslandDefinition.HeightLargeNoiseWeight),
            nameof(IslandDefinition.HeightMediumNoiseWeight),
            nameof(IslandDefinition.HeightFineNoiseWeight),
            nameof(IslandDefinition.HeightVoronoiRidgeWeight),
        ]),
        ("Voronoi / Biome Blend", ParameterFieldSource.Island,
        [
            nameof(IslandDefinition.WarpLargeStrength),
            nameof(IslandDefinition.WarpMediumStrength),
            nameof(IslandDefinition.WarpSmallStrength),
            nameof(IslandDefinition.BiomeBlendPower),
            nameof(IslandDefinition.BiomeBlendNeighborCount),
        ]),
        ("Tectonics", ParameterFieldSource.Island,
        [
            nameof(IslandDefinition.PlateMotionMin),
            nameof(IslandDefinition.PlateMotionMax),
            nameof(IslandDefinition.ContinentalCrustBias),
            nameof(IslandDefinition.SubductionUplift),
            nameof(IslandDefinition.CollisionUplift),
            nameof(IslandDefinition.DivergentRidgeBoost),
            nameof(IslandDefinition.ConvergenceThreshold),
        ]),
        ("Volcanic", ParameterFieldSource.Island,
        [
            nameof(IslandDefinition.MantlePlumeCount),
            nameof(IslandDefinition.MantlePlumeRadius),
            nameof(IslandDefinition.MantlePlumeIntensity),
            nameof(IslandDefinition.VolcanicConeCount),
            nameof(IslandDefinition.VolcanicConeRadius),
            nameof(IslandDefinition.VolcanicConeHeight),
            nameof(IslandDefinition.VolcanoProtectedCoreRadius),
            nameof(IslandDefinition.VolcanoRoadRingRadius),
            nameof(IslandDefinition.VolcanoRoadRingNodes),
            nameof(IslandDefinition.LavaFlowCount),
            nameof(IslandDefinition.LavaFlowMaxLength),
            nameof(IslandDefinition.LavaFlowWidth),
            nameof(IslandDefinition.LavaFlowMeanderStrength),
            nameof(IslandDefinition.LavaFlowTerminationRadius),
            nameof(IslandDefinition.LavaFlowRoadTraversalPenalty),
        ]),
        ("Erosion / Rivers", ParameterFieldSource.Island,
        [
            nameof(IslandDefinition.ErosionIterations),
            nameof(IslandDefinition.ErosionStrength),
            nameof(IslandDefinition.RiverCarveDepth),
            nameof(IslandDefinition.RiverCount),
            nameof(IslandDefinition.RiverMinElevation),
            nameof(IslandDefinition.RiverWidth),
            nameof(IslandDefinition.RiverMaxLength),
            nameof(IslandDefinition.RiverHeadSpacing),
        ]),
        ("Balance", ParameterFieldSource.Island,
        [
            nameof(IslandDefinition.MaxWetBiomeShare),
            nameof(IslandDefinition.MinElevationStdDev),
            nameof(IslandDefinition.BalancePassMaxIterations),
        ]),
        ("Facilities", ParameterFieldSource.Island,
        [
            nameof(IslandDefinition.DockCount),
            nameof(IslandDefinition.HelipadCount),
            nameof(IslandDefinition.HotelCount),
            nameof(IslandDefinition.RestaurantCount),
            nameof(IslandDefinition.AttractionCount),
            nameof(IslandDefinition.PaddockCount),
            nameof(IslandDefinition.MaintenanceAreaCount),
            nameof(IslandDefinition.RuinCount),
            nameof(IslandDefinition.FortificationCount),
            nameof(IslandDefinition.PaddockFenceRadius),
            nameof(IslandDefinition.TunnelCavernRadius),
        ]),
        ("Roads", ParameterFieldSource.Island,
        [
            nameof(IslandDefinition.RoadNetworkJunctionCount),
            nameof(IslandDefinition.RoadWidth),
            nameof(IslandDefinition.UseLegacyRandomRoads),
        ]),
        ("Biome Rules", ParameterFieldSource.BiomeRules,
        [
            nameof(BiomeRulesDefinition.OceanMaxElevation),
            nameof(BiomeRulesDefinition.BeachMaxElevation),
            nameof(BiomeRulesDefinition.MountainsMinElevation),
            nameof(BiomeRulesDefinition.SmallMountainMinElevation),
            nameof(BiomeRulesDefinition.HillsMinElevation),
            nameof(BiomeRulesDefinition.FoothillsMinElevation),
            nameof(BiomeRulesDefinition.SwampMinMoisture),
            nameof(BiomeRulesDefinition.ForestMinMoisture),
        ]),
    ];

    public static IReadOnlyList<ParameterFieldDescriptor> All { get; } = Build();

    public static IslandDefinition CloneIsland(IslandDefinition source)
    {
        return Clone<IslandDefinition>(source);
    }

    public static BiomeRulesDefinition CloneBiomeRules(BiomeRulesDefinition source)
    {
        return Clone<BiomeRulesDefinition>(source);
    }

    private static IReadOnlyList<ParameterFieldDescriptor> Build()
    {
        var islandProps = typeof(IslandDefinition)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => property.Name, StringComparer.Ordinal);
        var biomeProps = typeof(BiomeRulesDefinition)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => property.Name, StringComparer.Ordinal);

        var fields = new List<ParameterFieldDescriptor>();
        foreach ((string group, ParameterFieldSource source, string[] names) in Groups)
        {
            foreach (string name in names)
            {
                PropertyInfo property = source == ParameterFieldSource.Island
                    ? islandProps[name]
                    : biomeProps[name];

                fields.Add(new ParameterFieldDescriptor
                {
                    Group = group,
                    Name = name,
                    Label = ToLabel(name),
                    Kind = ToKind(property.PropertyType),
                    Source = source,
                    Property = property,
                });
            }
        }

        return fields;
    }

    private static T Clone<T>(T source)
        where T : new()
    {
        var clone = new T();
        foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.CanWrite)
            {
                property.SetValue(clone, property.GetValue(source));
            }
        }

        return clone;
    }

    private static ParameterFieldKind ToKind(Type type)
    {
        if (type == typeof(bool))
        {
            return ParameterFieldKind.Bool;
        }

        if (type == typeof(int))
        {
            return ParameterFieldKind.Int;
        }

        return ParameterFieldKind.Float;
    }

    private static string ToLabel(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(i == 0 ? char.ToUpper(c) : c);
        }

        return builder.ToString();
    }
}
