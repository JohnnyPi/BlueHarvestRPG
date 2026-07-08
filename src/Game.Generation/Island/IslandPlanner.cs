using Game.Simulation.World.Island;
using Game.Content.Definitions;
using Game.Generation.Island.Stages;

namespace Game.Generation.Island;

public sealed class IslandPlanner
{
    private readonly IslandDefinition _config;
    private readonly BiomeRulesDefinition _biomeRules;
    private readonly StructureBlueprintCatalog _blueprintCatalog;

    public IslandPlanner(
        IslandDefinition config,
        StructureBlueprintCatalog? blueprintCatalog = null,
        BiomeRulesDefinition? biomeRules = null)
    {
        _config = config;
        _biomeRules = biomeRules ?? new BiomeRulesDefinition();
        _blueprintCatalog = blueprintCatalog ?? StructureBlueprintCatalogDefaults.Create();
    }

    public IslandPlan Generate(int width, int height, ulong seed)
    {
        var plan = new IslandPlan(width, height, seed);

        IslandMaskStage.Execute(plan, _config, seed);
        VoronoiRegionStage.Execute(plan, _config, seed);
        TectonicPlateSetupStage.Execute(plan, _config, seed);
        LandmassStage.Execute(plan, _config, seed);
        TectonicBoundaryStage.Execute(plan, _config, seed);
        LandmassStage.Reconcile(plan, _config);
        VolcanicActivityStage.Execute(plan, _config, seed);
        ErosionStage.Execute(plan, _config, seed);
        LandmassStage.Reconcile(plan, _config);
        RegionBiomeStage.Execute(plan, _config, _biomeRules, seed);
        RoadNetworkStage.Execute(plan, _config, seed);
        ParkLayoutStage.Execute(plan, _config, seed);
        PaddockStage.Execute(plan, _config, seed);
        MaintenanceStage.Execute(plan, _config, seed);
        RuinsStage.Execute(plan, _config, seed);
        TunnelStage.Execute(plan, _config, seed);
        BiomeFinalizeStage.Execute(plan, seed);
        IslandBalanceStage.Execute(plan, _config, seed);
        StructureFinalizeStage.Execute(plan, seed, _blueprintCatalog);

        return plan;
    }
}
