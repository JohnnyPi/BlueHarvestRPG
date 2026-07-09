using Game.Simulation.AI;
using Game.Simulation.Combat;
using Game.Simulation.Coordinates;
using Game.Simulation.Ecology;
using Game.Simulation.Factions;
using Game.Simulation.Perception;
using Game.Simulation.Time;
using Game.Simulation.World;

namespace Game.Simulation.Entities;

public sealed class Entity
{
    public EntityId Id { get; init; }
    public EntityKind Kind { get; init; }
    public WorldCoord WorldPosition { get; set; }
    public LocalCoord LocalPosition { get; set; }
    public Direction Facing { get; set; } = Direction.East;
    public string? SpriteId { get; set; }
    public bool BlocksMovement { get; set; }
    public bool IsActive { get; set; }
    public ActorTurnState? Actor { get; set; }
    public FactionId Faction { get; set; }
    public int MaxHealth { get; set; }
    public int Health { get; set; }

    public RaptorMemory? Raptor { get; set; }

    public PerceptionState? Perception { get; set; }

    public CreatureDriveState? Drive { get; set; }

    public StatusEffectList? StatusEffects { get; set; }

    public int ImmobilizedTurns { get; set; }

    public bool IsAlive => MaxHealth <= 0 || Health > 0;
}
