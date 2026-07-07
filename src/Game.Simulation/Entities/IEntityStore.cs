using Game.Simulation.Coordinates;

namespace Game.Simulation.Entities;

public interface IEntityStore
{
    IReadOnlyList<Entity> All { get; }

    Entity? GetAt(LocalCoord position);

    Entity? GetById(EntityId id);

    void Add(Entity entity);

    bool Remove(EntityId id);

    bool BlocksMovementAt(LocalCoord position);
}
