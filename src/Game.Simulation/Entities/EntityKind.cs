namespace Game.Simulation.Entities;

public enum EntityKind
{
    Player,
    HarvestableTree,
    WanderingCreature,
    Raptor,
    Herbivore,
    Dilophosaur,
    SnareTrap,
    NoiseLure
}

public static class EntityKindExtensions
{
    public static Factions.FactionId DefaultFaction(this EntityKind kind)
    {
        return kind switch
        {
            EntityKind.Player => Factions.FactionId.Player,
            EntityKind.WanderingCreature => Factions.FactionId.Wildlife,
            EntityKind.Raptor => Factions.FactionId.Wildlife,
            EntityKind.Herbivore => Factions.FactionId.Wildlife,
            EntityKind.Dilophosaur => Factions.FactionId.Wildlife,
            _ => Factions.FactionId.Neutral
        };
    }
}
