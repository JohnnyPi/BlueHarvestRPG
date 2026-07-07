using Game.Simulation.Coordinates;

namespace Game.Simulation.Scenarios;

public sealed class IslandPressureState
{
    public int TravelStaminaPenalty { get; set; }

    public int? EvacHoursRemaining { get; set; }

    public bool PendingPredatorSpawn { get; set; }

    public bool MissedEvacuation { get; set; }

    public WorldCoord? HazardousTravelCell { get; set; }

    public void Restore(
        int travelStaminaPenalty,
        int? evacHoursRemaining,
        bool pendingPredatorSpawn,
        bool missedEvacuation,
        WorldCoord? hazardousTravelCell)
    {
        TravelStaminaPenalty = Math.Max(0, travelStaminaPenalty);
        EvacHoursRemaining = evacHoursRemaining;
        PendingPredatorSpawn = pendingPredatorSpawn;
        MissedEvacuation = missedEvacuation;
        HazardousTravelCell = hazardousTravelCell;
    }
}
