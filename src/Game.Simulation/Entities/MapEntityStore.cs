using Game.Simulation.Coordinates;

namespace Game.Simulation.Entities;

public sealed class MapEntityStore : IEntityStore
{
    private readonly List<Entity> _entities = [];

    public IReadOnlyList<Entity> All => _entities;

    public Entity? GetAt(LocalCoord position)
    {
        foreach (Entity entity in _entities)
        {
            if (entity.IsActive &&
                entity.LocalPosition.X == position.X &&
                entity.LocalPosition.Y == position.Y)
            {
                return entity;
            }
        }

        return null;
    }

    public Entity? GetById(EntityId id)
    {
        foreach (Entity entity in _entities)
        {
            if (entity.Id == id)
            {
                return entity;
            }
        }

        return null;
    }

    public void Add(Entity entity)
    {
        if (GetById(entity.Id) is not null)
        {
            throw new InvalidOperationException($"Entity {entity.Id.Value} already exists on this map.");
        }

        if (GetAt(entity.LocalPosition) is not null)
        {
            throw new InvalidOperationException($"Position {entity.LocalPosition} is already occupied.");
        }

        _entities.Add(entity);
    }

    public bool Remove(EntityId id)
    {
        for (int i = 0; i < _entities.Count; i++)
        {
            if (_entities[i].Id == id)
            {
                _entities.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public bool BlocksMovementAt(LocalCoord position)
    {
        Entity? entity = GetAt(position);
        return entity is not null && entity.BlocksMovement;
    }

    public void ReplaceAll(IEnumerable<Entity> entities)
    {
        _entities.Clear();
        var seenIds = new HashSet<ulong>();
        var seenPositions = new HashSet<(int X, int Y)>();

        foreach (Entity entity in entities)
        {
            if (!seenIds.Add(entity.Id.Value))
            {
                continue;
            }

            var key = (entity.LocalPosition.X, entity.LocalPosition.Y);
            if (!seenPositions.Add(key))
            {
                continue;
            }

            _entities.Add(entity);
        }
    }
}
