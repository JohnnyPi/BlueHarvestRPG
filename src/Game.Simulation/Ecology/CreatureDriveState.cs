namespace Game.Simulation.Ecology;

public sealed class CreatureDriveState
{
    public CreatureDrive ActiveDrive { get; set; } = CreatureDrive.Patrol;

    public int HungerCounter { get; set; }

    public ulong? HerdAnchorId { get; set; }

    public ulong? HuntTargetId { get; set; }
}
