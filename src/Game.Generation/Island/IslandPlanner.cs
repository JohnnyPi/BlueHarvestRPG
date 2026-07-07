using Game.Content.Definitions;
using Game.Generation.Island.Stages;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island;

public sealed class IslandPlanner
{
    private readonly IslandDefinition _config;

    public IslandPlanner(IslandDefinition config)
    {
        _config = config;
    }

    public IslandPlan Generate(int width, int height, ulong seed)
    {
        var plan = new IslandPlan(width, height, seed);

        VoronoiRegionStage.Execute(plan, _config, seed);
        TectonicPlateSetupStage.Execute(plan, _config, seed);
        LandmassStage.Execute(plan, _config, seed);
        TectonicBoundaryStage.Execute(plan, _config, seed);
        LandmassStage.Reconcile(plan, _config);
        VolcanicActivityStage.Execute(plan, _config, seed);
        RegionBiomeStage.Execute(plan, _config, seed);
        ParkLayoutStage.Execute(plan, _config, seed);
        PaddockStage.Execute(plan, _config, seed);
        MaintenanceStage.Execute(plan, _config, seed);
        RuinsStage.Execute(plan, _config, seed);
        TunnelStage.Execute(plan, _config, seed);
        BiomeFinalizeStage.Execute(plan, seed);
        IslandBalanceStage.Execute(plan, _config, seed);

        return plan;
    }
}
